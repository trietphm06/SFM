#!/usr/bin/env node
/**
 * SFM - Test & mô phỏng tự động.
 * Đọc thẳng <script> trong index.html (stub DOM), nên engine ở đây = engine chạy trên web.
 *
 * Chạy:  node simulate.js          (mặc định 1000 trận / kịch bản)
 *        node simulate.js 3000     (nhiều trận hơn -> số liệu mượt hơn)
 *        node simulate.js 500 --quick   (chỉ chạy kiểm tra logic, bỏ qua mô phỏng dài)
 * Thoát với mã 1 nếu có kiểm tra FAIL (dùng được trong CI).
 */
const fs = require('fs');
const path = require('path');

const N = parseInt(process.argv[2], 10) || 1000;
const QUICK = process.argv.includes('--quick');
const htmlPath = path.join(__dirname, 'index.html');

const html = fs.readFileSync(htmlPath, 'utf8');
const m = html.match(/<script>([\s\S]*)<\/script>/);
if (!m) { console.error('Không tìm thấy <script> trong index.html'); process.exit(2); }

// ---- stub DOM tối thiểu ----
const mk = () => new Proxy(function () {}, {
    get: (t, k) => k === 'value' ? '' : k === 'length' ? 0
        : k === 'querySelectorAll' ? () => [] : k === 'classList' ? { add() {}, remove() {}, toggle() {} } : mk(),
    set: () => true, apply: () => mk()
});
global.__ui = { formRed: { value: '4-4-2' }, formBlue: { value: '4-3-3' } };
global.document = { getElementById: id => global.__ui[id] || mk(), querySelectorAll: () => [], createElement: () => mk() };
global.localStorage = { getItem: () => null, setItem() {}, removeItem() {} };
global.confirm = () => true; global.alert = () => {};
global.requestAnimationFrame = () => 0; global.cancelAnimationFrame = () => 0;

// ---- phần chạy BÊN TRONG scope của app (truy cập trực tiếp hàm/biến của index.html) ----
function harness(OPTS) {
    const results = [];
    const check = (name, ok, detail) => { results.push({ name, ok, detail }); };
    const log = console.log;

    logMsg = function () {}; renderMatchSummary = function () {}; updateStatsHeader = function () {};

    function mkPlayers() {
        allPlayers = defaultDB.map(ii => ({
            id: Math.random().toString(36).slice(2), name: ii[0], pos: String(ii[1]).toUpperCase(),
            altPos: (ii[2] || '').toUpperCase().split(',').map(x => x.trim()).filter(Boolean),
            year: ii[3], season: ii[4], pac: ii[5], sho: ii[6], pas: ii[7], dri: ii[8], def: ii[9], phy: ii[10],
            div: ii[11], han: ii[12], kic: ii[13], ref: ii[14], spe: ii[15], pos_gk: ii[16], iq: ii[17], sta: ii[18],
            key: nameKey(ii[0]), seedX: 0, seedY: 0
        }));
        pool = [...allPlayers]; rosterRed = []; rosterBlue = []; subsRed = []; subsBlue = [];
        slotsRed = new Array(11).fill(null); slotsBlue = new Array(11).fill(null); draftTurn = 'red'; draftTurnCount = 1;
    }
    mkPlayers();

    // ===== 1. Dữ liệu =====
    check('DB: có 240 cầu thủ', defaultDB.length === 240, defaultDB.length);
    check('DB: cầu thủ sân không có chỉ số thủ môn', defaultDB.filter(r => r[1] !== 'GK').every(r => r.slice(11, 17).every(v => v === 0)));
    check('DB: thủ môn không có PAC/SHO/PAS/DRI/DEF', defaultDB.filter(r => r[1] === 'GK').every(r => r.slice(5, 10).every(v => v === 0)));
    let dupKeys = new Set(), dups = 0; defaultDB.forEach(r => { let k = r[0] + '|' + r[3]; if (dupKeys.has(k)) dups++; dupKeys.add(k); });
    check('DB: không trùng (tên, năm)', dups === 0, dups);

    // ===== 2. Khớp vị trí =====
    const P = (pos, alt = []) => ({ pos, altPos: alt });
    const fit = (p, r) => checkPositionFit(p, r);
    check('Fit: CDM đá LDM = chính', fit(P('CDM'), 'LDM').fit === 'exact');
    check('Fit: CF đá LST = phụ, không phạt', fit(P('CF', ['ST']), 'LST').penalty === 0);
    check('Fit: LB đá RB bị phạt', fit(P('LB'), 'RB').penalty > 0);
    check('Fit: CB đá ST bị phạt nặng (hệ số <= 0.6)', fit(P('CB'), 'ST').mult <= 0.6 + 1e-9);
    check('Fit: GK <-> sân bị phạt cực nặng', fit(P('GK'), 'ST').mult <= 0.34 && fit(P('ST'), 'GK').mult <= 0.34);

    // ===== 3. Draft =====
    let messi23 = pool.find(p => p.name === 'L. Messi' && p.year === '2023');
    turnPickPlayer(messi23.id, false);
    check('Draft: chọn Messi 2023 thì Messi 2022 biến mất', availablePool().filter(p => p.name === 'L. Messi').length === 0);
    let messi22 = pool.find(p => p.name === 'L. Messi');
    let before = rosterBlue.length; turnPickPlayer(messi22.id, false);
    check('Draft: đội kia không chọn được bản khác năm', rosterBlue.length === before);
    undraftPlayer(messi23.id, 'red', false);
    check('Draft: bỏ chọn thì trả lại kho đầy đủ', availablePool().length === 240);

    let aiOK = 0, aiUnique = 0, T = 100;
    for (let i = 0; i < T; i++) {
        mkPlayers();
        for (let k = 0; k < 22; k++) aiPickCurrentTurn();
        if (teamHasGK('red') && teamHasGK('blue')) aiOK++;
        let keys = [...rosterRed, ...rosterBlue].map(p => p.key); if (new Set(keys).size === keys.length) aiUnique++;
    }
    check('AI draft: 100% đội có thủ môn ở ô GK', aiOK === T, aiOK + '/' + T);
    check('AI draft: không trùng cầu thủ giữa 2 đội', aiUnique === T, aiUnique + '/' + T);

    // ===== 4. Đổi sơ đồ -> xếp lại =====
    let worseCount = 0, gkLost = 0, totBefore = 0, totAfter = 0, CASES = 60;
    const forms = Object.keys(FORMATION_SLOTS);
    for (let i = 0; i < CASES; i++) {
        mkPlayers();
        for (let k = 0; k < 22; k++) aiPickCurrentTurn();
        for (const to of forms) {
            __ui.formRed.value = to;
            let roles = FORMATION_SLOTS[to].roles;
            let pen = s => s.reduce((a, p, ix) => a + (p ? checkPositionFit(p, roles[ix]).penalty : 0), 0);
            let b = pen(slotsRed), keep = slotsRed.slice();
            autoArrange('red');
            let a = pen(slotsRed);
            totBefore += b; totAfter += a; if (a > b + 1e-9) worseCount++;
            if (!teamHasGK('red')) gkLost++;
            // giữ nguyên tập cầu thủ
            if (new Set(slotsRed.map(p => p.id)).size !== 11) worseCount++;
            slotsRed = keep; // khôi phục để so sánh sơ đồ kế tiếp từ cùng 1 đội hình
        }
    }
    check('Đổi sơ đồ: xếp lại không bao giờ tệ hơn', worseCount === 0, worseCount);
    check('Đổi sơ đồ: thủ môn luôn ở ô GK', gkLost === 0, gkLost);
    check('Đổi sơ đồ: giảm tổng điểm phạt đáng kể', totAfter < totBefore * 0.7 || totBefore === 0, totBefore + ' -> ' + totAfter);

    if (OPTS.quick) return { results };

    // ===== 5. Mô phỏng trận =====
    function candidates(role, used) { return allPlayers.filter(p => !used.has(p.key) && checkPositionFit(p, role).penalty === 0); }
    function pickFor(role, used, mode) {
        let c = candidates(role, used).sort((a, b) => calcOVR(b) - calcOVR(a));
        if (!c.length) c = allPlayers.filter(p => !used.has(p.key)).sort((a, b) => checkPositionFit(a, role).penalty - checkPositionFit(b, role).penalty || calcOVR(b) - calcOVR(a));
        let p = mode === 'best' ? c[0] : c[Math.floor(c.length / 2 + Math.random() * Math.ceil(c.length / 2)) % c.length];
        used.add(p.key); return p;
    }
    function buildTeams(fR, fB, mR, mB, redFirst) {
        let used = new Set(), r = [], b = [], rr = FORMATION_SLOTS[fR].roles, br = FORMATION_SLOTS[fB].roles;
        for (let i = 0; i < 11; i++) {
            if (redFirst) { r[i] = pickFor(rr[i], used, mR); b[i] = pickFor(br[i], used, mB); }
            else { b[i] = pickFor(br[i], used, mB); r[i] = pickFor(rr[i], used, mR); }
        }
        return { r, b };
    }
    function playMatch() {
        sentOffRed = []; sentOffBlue = [];
        slotsRed.concat(slotsBlue).forEach(p => { p.sentOff = false; p.stamina = 100; p.goals = p.assists = p.shots = p.saves = p.tackles = p.passes = p.dribbles = p.fouls = 0; });
        scoreRed = 0; scoreBlue = 0; currentMinute = 0; half = 1; isPaused = false;
        shotsRed = shotsBlue = onTargetRed = onTargetBlue = passesRed = passesBlue = 0;
        extra1 = Math.floor(Math.random() * 5) + 1; extra2 = Math.floor(Math.random() * 5) + 1;
        matchSummary = { redScorers: [], blueScorers: [] };
        for (;;) { simulateMinute(); if (half === 2 && currentMinute === 90 + extra2) break; }
        // kiểm tra tính toàn vẹn thẻ đỏ
        const cnt = a => a.filter(Boolean).length;
        const reds = sentOffRed.length + sentOffBlue.length;
        redStat.matches++; redStat.reds += reds; if (reds) redStat.withRed++;
        if (cnt(slotsRed) !== 11 - sentOffRed.length || cnt(slotsBlue) !== 11 - sentOffBlue.length) redStat.badCount++;
        if (cnt(slotsRed) < 7 || cnt(slotsBlue) < 7) redStat.below7++;
        sentOffRed.concat(sentOffBlue).forEach(o => {
            let p = o.p, now = (p.goals || 0) + (p.assists || 0) + (p.shots || 0) + (p.passes || 0) + (p.tackles || 0) + (p.dribbles || 0);
            if (now !== p.snap) redStat.actedAfter++;
            if (slotsRed.includes(p) || slotsBlue.includes(p)) redStat.stillOn++;
        });
        return { gr: scoreRed, gb: scoreBlue, sr: shotsRed, sb: shotsBlue, tr: onTargetRed, tb: onTargetBlue };
    }
    const redStat = { matches: 0, reds: 0, withRed: 0, badCount: 0, below7: 0, actedAfter: 0, stillOn: 0 };
    const avgOVR = s => s.reduce((a, p) => a + calcOVR(p), 0) / s.length;

    function scenario(name, fR, fB, mR, mB, scramble) {
        __ui.formRed.value = fR; __ui.formBlue.value = fB;
        let a = { gr: 0, gb: 0, sr: 0, sb: 0, tr: 0, tb: 0, rw: 0, bw: 0, d: 0, zero: 0, hi: 0 }, ovrR = 0, ovrB = 0;
        for (let i = 0; i < OPTS.n; i++) {
            let t = buildTeams(fR, fB, mR, mB, i % 2 === 0), rs = t.r.slice();
            if (scramble) { let out = rs.slice(1); for (let k = out.length - 1; k > 0; k--) { let j = Math.floor(Math.random() * (k + 1)); [out[k], out[j]] = [out[j], out[k]]; } rs = [rs[0], ...out]; }
            slotsRed = rs; slotsBlue = t.b; ovrR += avgOVR(rs); ovrB += avgOVR(t.b);
            let r = playMatch();
            a.gr += r.gr; a.gb += r.gb; a.sr += r.sr; a.sb += r.sb; a.tr += r.tr; a.tb += r.tb;
            if (r.gr > r.gb) a.rw++; else if (r.gr < r.gb) a.bw++; else a.d++;
            if (r.gr + r.gb === 0) a.zero++; if (r.gr + r.gb >= 7) a.hi++;
        }
        const n = OPTS.n, pc = x => (100 * x / n);
        let o = { name, goals: (a.gr + a.gb) / n, gr: a.gr / n, gb: a.gb / n, shots: (a.sr + a.sb) / n, sot: (a.tr + a.tb) / n,
            conv: 100 * (a.gr + a.gb) / (a.sr + a.sb), rw: pc(a.rw), d: pc(a.d), bw: pc(a.bw), zero: pc(a.zero), hi: pc(a.hi), ovrR: ovrR / n, ovrB: ovrB / n };
        log('\n== ' + name + ' (' + n + ' trận) ==');
        log('OVR Đỏ/Xanh ' + o.ovrR.toFixed(1) + '/' + o.ovrB.toFixed(1) + ' | Bàn/trận ' + o.goals.toFixed(2) + ' (Đỏ ' + o.gr.toFixed(2) + ' - Xanh ' + o.gb.toFixed(2) + ')'
            + ' | Sút ' + o.shots.toFixed(1) + ' (trúng ' + o.sot.toFixed(1) + ') | Bàn/sút ' + o.conv.toFixed(1) + '%');
        log('Đỏ thắng ' + o.rw.toFixed(1) + '% | Hoà ' + o.d.toFixed(1) + '% | Xanh thắng ' + o.bw.toFixed(1) + '% | 0-0: ' + o.zero.toFixed(1) + '% | >=7 bàn: ' + o.hi.toFixed(1) + '%');
        return o;
    }

    let A = scenario('Gương 4-4-2 vs 4-4-2 (đội mạnh)', '4-4-2', '4-4-2', 'best', 'best', false);
    let A2 = scenario('4-4-2 vs 4-3-3', '4-4-2', '4-3-3', 'best', 'best', false);
    let A3 = scenario('3-5-2 vs 4-2-3-1', '3-5-2', '4-2-3-1', 'best', 'best', false);
    let B = scenario('Đội mạnh vs đội nửa dưới danh sách', '4-4-2', '4-4-2', 'best', 'weak', false);
    let C = scenario('Xếp đúng vị trí vs đội mạnh', '4-4-2', '4-4-2', 'best', 'best', false);
    let C2 = scenario('Cùng đội đó, xáo ngẫu nhiên vị trí', '4-4-2', '4-4-2', 'best', 'best', true);

    // Kịch bản: Đỏ chỉ có 10 người từ phút 0 (mô phỏng thẻ đỏ sớm)
    function scenario10() {
        __ui.formRed.value = '4-4-2'; __ui.formBlue.value = '4-4-2';
        let w = 0, l = 0, d = 0, g = 0, gb = 0, n = OPTS.n;
        for (let i = 0; i < n; i++) {
            let t = buildTeams('4-4-2', '4-4-2', 'best', 'best', i % 2 === 0);
            slotsRed = t.r.slice(); slotsBlue = t.b;
            let r = (function () {
                slotsRed.concat(slotsBlue).forEach(p => { p.sentOff = false; });
                // bỏ 1 tiền vệ trung tâm trước trận
                let k = slotsRed.findIndex((p, ix) => FORMATION_SLOTS['4-4-2'].roles[ix] === 'LCM');
                let removed = slotsRed[k]; slotsRed[k] = null;
                let rr = playMatchNoReset(removed);
                return rr;
            })();
            g += r.gr; gb += r.gb;
            if (r.gr > r.gb) w++; else if (r.gr < r.gb) l++; else d++;
        }
        log('\n== Đỏ thiếu 1 người từ phút 0 (' + n + ' trận) ==');
        log('Đỏ thắng ' + (100 * w / n).toFixed(1) + '% | Hoà ' + (100 * d / n).toFixed(1) + '% | Xanh thắng ' + (100 * l / n).toFixed(1) + '% | Bàn Đỏ ' + (g / n).toFixed(2) + ' - Xanh ' + (gb / n).toFixed(2));
        return { rw: 100 * w / n, bw: 100 * l / n };
    }
    function playMatchNoReset(removed) {
        sentOffRed = []; sentOffBlue = [];
        slotsRed.concat(slotsBlue).forEach(p => { if (!p) return; p.stamina = 100; p.goals = p.assists = p.shots = p.saves = p.tackles = p.passes = p.dribbles = p.fouls = 0; });
        scoreRed = 0; scoreBlue = 0; currentMinute = 0; half = 1; isPaused = false;
        shotsRed = shotsBlue = onTargetRed = onTargetBlue = passesRed = passesBlue = 0;
        extra1 = Math.floor(Math.random() * 5) + 1; extra2 = Math.floor(Math.random() * 5) + 1;
        matchSummary = { redScorers: [], blueScorers: [] };
        for (;;) { simulateMinute(); if (half === 2 && currentMinute === 90 + extra2) break; }
        return { gr: scoreRed, gb: scoreBlue };
    }
    let T10 = scenario10();
    log('\nThẻ đỏ tự nhiên: ' + (redStat.reds / redStat.matches).toFixed(3) + ' thẻ/trận, ' + (100 * redStat.withRed / redStat.matches).toFixed(1) + '% trận có thẻ đỏ');

    // ===== 6. Kiểm tra cân bằng =====
    const band = (name, v, lo, hi) => check(name, v >= lo && v <= hi, v.toFixed(2) + ' (cần ' + lo + '..' + hi + ')');
    band('Cân bằng: bàn/trận (gương) trong 2.2..3.3', A.goals, 2.2, 3.3);
    band('Cân bằng: bàn/trận (4-4-2 vs 4-3-3) trong 2.2..3.3', A2.goals, 2.2, 3.3);
    band('Cân bằng: bàn/trận (3-5-2 vs 4-2-3-1) trong 2.2..3.3', A3.goals, 2.2, 3.3);
    band('Cân bằng: tỉ lệ 0-0 trong 2%..12%', A.zero, 2, 12);
    band('Cân bằng: >=7 bàn dưới 8%', A.hi, 0, 8);
    band('Cân bằng: gương không lệch Đỏ/Xanh quá 10 điểm', Math.abs(A.rw - A.bw), 0, 10 + (N < 1000 ? 6 : 0));
    check('Cân bằng: đội mạnh hơn thắng nhiều hơn thua', B.rw > B.bw + 5, B.rw.toFixed(1) + ' vs ' + B.bw.toFixed(1));
    check('Vị trí: xáo vị trí thua thiệt rõ (thắng ít hơn >= 4 điểm)', C.rw - C2.rw >= 4 - (N < 1000 ? 3 : 0), (C.rw - C2.rw).toFixed(1));

    check('Thẻ đỏ: số người trên sân = 11 - số thẻ đỏ', redStat.badCount === 0, redStat.badCount);
    check('Thẻ đỏ: không đội nào dưới 7 người', redStat.below7 === 0, redStat.below7);
    check('Thẻ đỏ: cầu thủ bị đuổi không còn hành động sau đó', redStat.actedAfter === 0 && redStat.stillOn === 0, redStat.actedAfter + '/' + redStat.stillOn);
    band('Thẻ đỏ: tần suất 0.03..0.5 thẻ/trận', redStat.reds / redStat.matches, 0.03, 0.5);
    check('Thẻ đỏ: đội thiếu người thắng ít hơn rõ rệt (Đỏ thắng < Xanh thắng - 8)', T10.rw < T10.bw - 8, T10.rw.toFixed(1) + ' vs ' + T10.bw.toFixed(1));
    check('Thẻ đỏ: đội 10 người thắng ít hơn đội đủ người >= 5 điểm', A.rw - T10.rw >= 5 - (N < 1000 ? 3 : 0), (A.rw - T10.rw).toFixed(1));

    return { results };
}

const out = eval(m[1] + '\n;(' + harness.toString() + ')(' + JSON.stringify({ n: N, quick: QUICK }) + ');');

console.log('\n================ KẾT QUẢ KIỂM TRA ================');
let fail = 0;
for (const r of out.results) {
    console.log((r.ok ? '  PASS ' : '  FAIL ') + r.name + (r.detail !== undefined ? '   [' + r.detail + ']' : ''));
    if (!r.ok) fail++;
}
console.log('\n' + (fail ? fail + ' kiểm tra FAIL' : 'Tất cả ' + out.results.length + ' kiểm tra PASS'));
process.exit(fail ? 1 : 0);
