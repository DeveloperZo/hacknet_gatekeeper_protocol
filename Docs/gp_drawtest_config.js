'use strict';
// ============================================================
// GP DrawTest — Shared Config, Constants & Utilities
// Mirrors GatekeeperProtocol.cs values exactly.
// All HTML files import this; nothing here touches the DOM.
// ============================================================

const GP_CONFIG_KEY = 'gp_drawtest_params';

// Defaults match DrawTestParams in the plugin
const GP_DEFAULTS = {
  headerH:     28,
  targetRows:  6,
  ramCostV2:   256,
  ramCostV3:   384,
  accentH:     3,       // accent bar height at bottom of header
  labelOffset: 11,      // header text baseline = headerH - labelOffset
  charW:       6,       // approximate tinyfont advance width
  charH:       11,      // approximate tinyfont line height
  // Draw style per exe family — mirrors DrawTestParams in GatekeeperProtocol.cs
  sshStyle:    'matrix', // "matrix" | "packets"
  ftpStyle:    'packets',
  webStyle:    'matrix',
};

// Mirrors each concrete cracker class: GPSSHCrackV2, GPFTPCrackV2 ... GPWebCrackV3
// All tiers share the same port numbers as vanilla (22/21/80).
// Tier is encoded in protocol name (ssh_v2 / ssh_v3), not the number.
const GP_CRACKERS = {
  ssh_v2: { tier:2, label:'SSHcrack_v2',      port:22, solveTime:10, key:null             },
  ftp_v2: { tier:2, label:'FTPBounce_v2',     port:21, solveTime:10, key:null             },
  web_v2: { tier:2, label:'WebServerWorm_v2', port:80, solveTime:10, key:null             },
  ssh_v3: { tier:3, label:'SSHcrack_v3',      port:22, solveTime:15, key:'ssh_v3_key.dat' },
  ftp_v3: { tier:3, label:'FTPBounce_v3',     port:21, solveTime:15, key:'ftp_v3_key.dat' },
  web_v3: { tier:3, label:'WebServerWorm_v3', port:80, solveTime:15, key:'web_v3_key.dat' },
};

// Mirrors TIER_BAR (cracked glyph color) and TIER_LABEL in GatekeeperProtocol.cs
const GP_TIER_CRACKED = {
  2: { r:200, g:120, b:0   },   // V2 orange
  3: { r:0,   g:180, b:220 },   // V3 cyan
};
const GP_TIER_LABEL_COLOR = {
  2: { r:255, g:180, b:80  },   // V2 amber
  3: { r:80,  g:200, b:255 },   // V3 sky
};

// Uncracked glyph color — same across all tiers (Color(180,0,0) in C#)
const GP_UNCRACKED  = { r:180, g:0, b:0 };
const GP_HEX_CHARS  = '0123456789ABCDEF';

// ---- Config persistence (localStorage) ----------------------------------------

function gpLoadConfig() {
  try {
    const stored = JSON.parse(localStorage.getItem(GP_CONFIG_KEY) || '{}');
    return Object.assign({}, GP_DEFAULTS, stored);
  } catch { return { ...GP_DEFAULTS }; }
}

function gpSaveConfig(cfg) {
  try { localStorage.setItem(GP_CONFIG_KEY, JSON.stringify(cfg)); } catch (_) {}
}

// ---- Utilities -----------------------------------------------------------------

function gpToRgb(c, a = 1) { return `rgba(${c.r},${c.g},${c.b},${a})`; }
function gpRndHex()         { return GP_HEX_CHARS[Math.random() * 16 | 0]; }

// Mirrors the auto-scale logic in GPCrackBase.Draw().
// Returns [cols, rows, scale, CHAR_W, CHAR_H].
function gpGetColsRows(panelW, panelH, cfg) {
  const cW  = panelW - 8;
  const cH  = panelH - cfg.headerH - 4;
  const fW  = cfg.charW;
  const fH  = cfg.charH;
  if (cH <= 0) {
    const minCW = Math.max(1, (fW * 0.45) | 0);
    const minCH = Math.max(1, (fH * 0.45) | 0);
    return [1, 0, 0.45, minCW, minCH];
  }
  const scale  = Math.min(1.0, Math.max(0.45, cH / (cfg.targetRows * fH)));
  const CHAR_W = Math.max(1, (fW * scale) | 0);
  const CHAR_H = Math.max(1, (fH * scale) | 0);
  const cols   = Math.max(1, (cW / CHAR_W) | 0);
  const rows   = Math.min(cfg.targetRows, Math.max(1, (cH / CHAR_H) | 0));
  return [cols, rows, scale, CHAR_W, CHAR_H];
}

// Compute module pixel height: ramCost / playerRam * panelFullH
function gpModuleH(cfg, tier, playerRam, panelFullH) {
  const ramCost = tier >= 3 ? cfg.ramCostV3 : cfg.ramCostV2;
  return Math.max(20, Math.round(ramCost / playerRam * panelFullH));
}

// Draw the module header bar (mirrors base.Draw + drawOutline in C#).
function gpDrawHeader(ctx, cracker, cfg, panelW) {
  const h           = cfg.headerH;
  const accentH     = cfg.accentH     ?? 3;
  const labelOffset = cfg.labelOffset ?? 11;
  const cH          = cfg.charH;
  const cc = GP_TIER_CRACKED[cracker.tier];
  const lc = GP_TIER_LABEL_COLOR[cracker.tier];
  ctx.fillStyle = '#12121e';
  ctx.fillRect(0, 0, panelW, h);
  ctx.fillStyle = gpToRgb(cc, 0.7);
  ctx.fillRect(0, h - accentH, panelW, accentH);
  ctx.font = `${Math.max(9, cH - 2)}px monospace`;
  ctx.fillStyle = gpToRgb(lc, 0.9);
  ctx.fillText(cracker.label, 6, h - labelOffset);
  ctx.fillStyle = gpToRgb(cc, 0.8);
  ctx.fillText(':' + cracker.port, panelW - 44, h - labelOffset);
  if (cracker.key) {
    ctx.font = '8px monospace';
    ctx.fillStyle = gpToRgb({ r:80, g:200, b:255 }, 0.7);
    ctx.fillText('[KEY]', panelW - 36, 9);
  }
}

function gpDrawOutline(ctx, tier, panelW, panelH) {
  ctx.strokeStyle = gpToRgb(GP_TIER_CRACKED[tier], 0.4);
  ctx.lineWidth   = 1;
  ctx.strokeRect(0.5, 0.5, panelW - 1, panelH - 1);
}

// Initialize a hex grid (vanilla SSHcrack-style random threshold assignment).
// Each canvas (V2/V3) gets its own independent state object.
function gpInitGrid(cols, rows) {
  const grid = [], threshold = [];
  for (let r = 0; r < rows; r++) {
    grid.push([]); threshold.push([]);
    for (let c = 0; c < cols; c++) {
      grid[r].push(gpRndHex());
      threshold[r].push(Math.random());
    }
  }
  return { grid, threshold, timer: 0 };
}

// Standard flicker tick — uncracked cells scramble at ~12 Hz (mirrors 0.08f interval in C#).
function gpTickFlicker(state, dt, cols, rows, pct) {
  state.timer += dt;
  if (state.timer >= 0.08) {
    state.timer = 0;
    for (let r = 0; r < rows; r++)
      for (let c = 0; c < cols; c++)
        if (state.threshold[r][c] > pct)
          state.grid[r][c] = gpRndHex();
  }
}

// Standard matrix draw — threshold-based cracked/uncracked coloring.
function gpDrawMatrix(ctx, state, cols, rows, pct, cX, cY, cW, cH, accent) {
  ctx.font = `${cH - 1}px monospace`;
  for (let r = 0; r < rows; r++)
    for (let c = 0; c < cols; c++) {
      ctx.fillStyle = state.threshold[r][c] <= pct
        ? gpToRgb(accent)
        : gpToRgb(GP_UNCRACKED);
      ctx.fillText(state.grid[r][c], cX + c * cW, cY + r * cH + cH - 2);
    }
}

// Packets draw — rows complete top-to-bottom, columns left-to-right.
// Mirrors DrawPackets() in GatekeeperProtocol.cs.
function gpDrawPackets(ctx, state, cols, rows, pct, cX, cY, cW, cH, accent) {
  const crackedRows = (pct * rows) | 0;
  const crackedCols = ((pct * rows - crackedRows) * cols) | 0;
  ctx.font = `${cH - 1}px monospace`;
  for (let r = 0; r < rows; r++)
    for (let c = 0; c < cols; c++) {
      let color;
      if      (r < crackedRows || (r === crackedRows && c < crackedCols)) color = gpToRgb(accent);
      else if (r === crackedRows && c === crackedCols)                    color = 'rgba(255,255,255,0.9)';
      else                                                                color = gpToRgb(GP_UNCRACKED);
      ctx.fillStyle = color;
      ctx.fillText(state.grid[r][c], cX + c * cW, cY + r * cH + cH - 2);
    }
}

// Waveform draw — sine-wave sweep, left-to-right per row with vertical ripple.
// Mirrors DrawWaveform() in GatekeeperProtocol.cs.
function gpDrawWaveform(ctx, state, cols, rows, pct, cX, cY, cW, cH, accent) {
  ctx.font = `${cH - 1}px monospace`;
  for (let r = 0; r < rows; r++) {
    const rowPhase = r / rows * 0.3;
    for (let c = 0; c < cols; c++) {
      const colPct = c / cols;
      const front  = pct + 0.08 * Math.sin(r * 1.5 + pct * 6.28) - rowPhase;
      let color;
      if      (colPct <= front - 0.06) color = gpToRgb(accent);
      else if (colPct <= front)        color = 'rgba(255,255,255,0.9)'; // leading edge
      else                             color = gpToRgb(GP_UNCRACKED);
      ctx.fillStyle = color;
      ctx.fillText(state.grid[r][c], cX + c * cW, cY + r * cH + cH - 2);
    }
  }
}

// Waveform flicker — only cells ahead of the wave front randomise.
function gpTickFlickerWaveform(state, dt, cols, rows, pct) {
  state.timer += dt;
  if (state.timer >= 0.08) {
    state.timer = 0;
    for (let r = 0; r < rows; r++) {
      const rowPhase = r / rows * 0.3;
      for (let c = 0; c < cols; c++) {
        const front = pct + 0.08 * Math.sin(r * 1.5 + pct * 6.28) - rowPhase;
        if (c / cols > front) state.grid[r][c] = gpRndHex();
      }
    }
  }
}

// Packets flicker — only uncracked region randomises each tick.
function gpTickFlickerPackets(state, dt, cols, rows, pct) {
  state.timer += dt;
  if (state.timer >= 0.08) {
    state.timer = 0;
    const cR = (pct * rows) | 0;
    const cC = ((pct * rows - cR) * cols) | 0;
    for (let r = 0; r < rows; r++)
      for (let c = 0; c < cols; c++)
        if (r > cR || (r === cR && c >= cC))
          state.grid[r][c] = gpRndHex();
  }
}
