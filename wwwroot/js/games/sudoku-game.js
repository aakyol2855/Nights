/**
 * DİVAN SUDOKU - %100 İstemci Taraflı (Vanilla JS) Tek Çözümlü Sudoku Motoru
 * 4 Zorluk Seviyesi (Kolay, Orta, Zor, İmkansız) | Kalem Modu | Undo | Klavye & Mobil Uyumlu
 */

document.addEventListener('DOMContentLoaded', () => {
    const mount = document.getElementById('gameBoardMount');
    const diffBar = document.getElementById('gameDifficultySelector');
    if (!mount) return;

    const DIFF_CONFIG = {
        easy: { label: 'Kolay', clues: 40, multiplier: 1.0, targetSec: 360 },
        medium: { label: 'Orta', clues: 33, multiplier: 1.25, targetSec: 600 },
        hard: { label: 'Zor', clues: 27, multiplier: 1.5, targetSec: 900 },
        impossible: { label: 'İmkansız', clues: 23, multiplier: 2.0, targetSec: 1500 }
    };

    // --- Bitmask + MRV Tek Çözümlü Sudoku Üretici ---
    const shuffle = (arr) => {
        for (let i = arr.length - 1; i > 0; i--) {
            const j = Math.floor(Math.random() * (i + 1));
            [arr[i], arr[j]] = [arr[j], arr[i]];
        }
        return arr;
    };

    const boxIdx = (r, c) => Math.floor(r / 3) * 3 + Math.floor(c / 3);

    const popCount9 = (mask) => {
        let c = 0;
        let m = mask;
        while (m) {
            m &= (m - 1);
            c++;
        }
        return c;
    };

    const countSolutions = (board, limit = 2) => {
        const rowMask = new Int32Array(9);
        const colMask = new Int32Array(9);
        const blkMask = new Int32Array(9);

        for (let i = 0; i < 81; i++) {
            const v = board[i];
            if (v !== 0) {
                const bit = 1 << (v - 1);
                const r = Math.floor(i / 9);
                const c = i % 9;
                const b = boxIdx(r, c);
                if ((rowMask[r] & bit) || (colMask[c] & bit) || (blkMask[b] & bit)) {
                    return 0;
                }
                rowMask[r] |= bit;
                colMask[c] |= bit;
                blkMask[b] |= bit;
            }
        }

        const work = board.slice();
        let solutions = 0;

        const dfs = () => {
            if (solutions >= limit) return;

            // MRV: En az adayı olan boş hücreyi seç
            let bestIdx = -1;
            let bestCandMask = 0;
            let bestCount = 10;

            for (let i = 0; i < 81; i++) {
                if (work[i] !== 0) continue;
                const r = Math.floor(i / 9);
                const c = i % 9;
                const b = boxIdx(r, c);
                const used = rowMask[r] | colMask[c] | blkMask[b];
                const cand = (~used) & 0x1ff;
                if (cand === 0) return;

                const cnt = popCount9(cand);
                if (cnt < bestCount) {
                    bestCount = cnt;
                    bestIdx = i;
                    bestCandMask = cand;
                    if (cnt === 1) break;
                }
            }

            if (bestIdx === -1) {
                solutions++;
                return;
            }

            const r = Math.floor(bestIdx / 9);
            const c = bestIdx % 9;
            const b = boxIdx(r, c);

            for (let d = 1; d <= 9; d++) {
                const bit = 1 << (d - 1);
                if (bestCandMask & bit) {
                    work[bestIdx] = d;
                    rowMask[r] |= bit;
                    colMask[c] |= bit;
                    blkMask[b] |= bit;

                    dfs();

                    work[bestIdx] = 0;
                    rowMask[r] &= ~bit;
                    colMask[c] &= ~bit;
                    blkMask[b] &= ~bit;

                    if (solutions >= limit) return;
                }
            }
        };

        dfs();
        return solutions;
    };

    const generateSolvedBoard = () => {
        const grid = new Array(81).fill(0);

        // Köşegen 3x3 kutuları birbirinden bağımsız rastgele doldur
        for (let k = 0; k < 9; k += 3) {
            const nums = shuffle([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            let p = 0;
            for (let r = k; r < k + 3; r++) {
                for (let c = k; c < k + 3; c++) {
                    grid[r * 9 + c] = nums[p++];
                }
            }
        }

        const isSafe = (idx, val) => {
            const r = Math.floor(idx / 9);
            const c = idx % 9;
            for (let i = 0; i < 9; i++) {
                if (grid[r * 9 + i] === val || grid[i * 9 + c] === val) return false;
            }
            const br = Math.floor(r / 3) * 3;
            const bc = Math.floor(c / 3) * 3;
            for (let rr = br; rr < br + 3; rr++) {
                for (let cc = bc; cc < bc + 3; cc++) {
                    if (grid[rr * 9 + cc] === val) return false;
                }
            }
            return true;
        };

        const fillRest = (idx) => {
            while (idx < 81 && grid[idx] !== 0) idx++;
            if (idx >= 81) return true;

            const digits = shuffle([1, 2, 3, 4, 5, 6, 7, 8, 9]);
            for (const d of digits) {
                if (isSafe(idx, d)) {
                    grid[idx] = d;
                    if (fillRest(idx + 1)) return true;
                    grid[idx] = 0;
                }
            }
            return false;
        };

        fillRest(0);
        return grid;
    };

    const generateSudokuPuzzle = (targetClues) => {
        const solution = generateSolvedBoard();
        const puzzle = solution.slice();
        const positions = shuffle(Array.from({ length: 81 }, (_, i) => i));
        let remainingClues = 81;

        for (const pos of positions) {
            if (remainingClues <= targetClues) break;
            const backup = puzzle[pos];
            puzzle[pos] = 0;

            if (countSolutions(puzzle, 2) === 1) {
                remainingClues--;
            } else {
                puzzle[pos] = backup;
            }
        }

        return { puzzle, solution };
    };

    // --- Oyun Durumu ---
    let currentDiffKey = 'easy';
    let solutionGrid = new Array(81).fill(0);
    let givenMask = new Array(81).fill(false);
    let userGrid = new Array(81).fill(0);
    let notesMask = new Int32Array(81); // Her hücre için 9-bit kalem notu maskesi
    let errorCellIndex = -1;

    let selectedIdx = -1;
    let isNotesMode = false;
    let mistakes = 0;
    let elapsedSeconds = 0;
    let isGameOver = false;
    let timerId = null;
    let undoStack = [];

    // --- Arayüz İskeletini Oluştur ---
    mount.textContent = '';

    const hud = document.createElement('div');
    hud.className = 'sudoku-hud';
    hud.innerHTML = `
        <div class="sudoku-hud-item">
            <span class="sudoku-hud-label">Canlı Skor</span>
            <span class="sudoku-hud-value" id="sudokuHudScore">1000</span>
        </div>
        <div class="sudoku-hud-item">
            <span class="sudoku-hud-label">Hata Sayısı</span>
            <span class="sudoku-hud-value" id="sudokuHudMistakes">0</span>
        </div>
        <div class="sudoku-hud-item">
            <span class="sudoku-hud-label">Kronometre</span>
            <span class="sudoku-hud-value" id="sudokuHudTimer">00:00</span>
        </div>
    `;

    const boardWrap = document.createElement('div');
    boardWrap.className = 'sudoku-board-wrap';
    const boardEl = document.createElement('div');
    boardEl.className = 'sudoku-board';
    boardEl.setAttribute('role', 'grid');
    boardEl.setAttribute('aria-label', '9x9 Sudoku Izgarası');
    boardWrap.appendChild(boardEl);

    const toolsEl = document.createElement('div');
    toolsEl.className = 'sudoku-tools';
    toolsEl.innerHTML = `
        <button type="button" class="sudoku-tool-btn" id="btnSudokuNotes" title="Klavye: N veya P">
            <span>✎ Kalem</span>
            <small id="lblNotesState" style="font-size:0.65rem; opacity:0.8;">KAPALI</small>
        </button>
        <button type="button" class="sudoku-tool-btn" id="btnSudokuUndo" title="Klavye: Z">
            <span>↶ Geri Al</span>
            <small style="font-size:0.65rem; opacity:0.8;">Hamle</small>
        </button>
        <button type="button" class="sudoku-tool-btn" id="btnSudokuErase" title="Klavye: Sil / Backspace">
            <span>⌫ Sil</span>
            <small style="font-size:0.65rem; opacity:0.8;">Temizle</small>
        </button>
        <button type="button" class="sudoku-tool-btn" id="btnSudokuNewGame">
            <span>↻ Yeni</span>
            <small style="font-size:0.65rem; opacity:0.8;">Tahta</small>
        </button>
    `;

    const numpadEl = document.createElement('div');
    numpadEl.className = 'sudoku-numpad';
    const numButtons = [];
    for (let d = 1; d <= 9; d++) {
        const btn = document.createElement('button');
        btn.type = 'button';
        btn.className = 'sudoku-num-btn';
        btn.dataset.digit = String(d);

        const mainSpan = document.createElement('span');
        mainSpan.className = 'num-main';
        mainSpan.textContent = String(d);

        const remSpan = document.createElement('span');
        remSpan.className = 'num-rem';
        remSpan.textContent = '9';

        btn.appendChild(mainSpan);
        btn.appendChild(remSpan);
        numpadEl.appendChild(btn);
        numButtons.push({ btn, remSpan, digit: d });
    }

    mount.appendChild(hud);
    mount.appendChild(boardWrap);
    mount.appendChild(toolsEl);
    mount.appendChild(numpadEl);

    const hudScoreEl = document.getElementById('sudokuHudScore');
    const hudMistakesEl = document.getElementById('sudokuHudMistakes');
    const hudTimerEl = document.getElementById('sudokuHudTimer');
    const btnNotes = document.getElementById('btnSudokuNotes');
    const lblNotesState = document.getElementById('lblNotesState');
    const btnUndo = document.getElementById('btnSudokuUndo');
    const btnErase = document.getElementById('btnSudokuErase');
    const btnNewGame = document.getElementById('btnSudokuNewGame');

    // 81 Hücre DOM Elemanlarını Oluştur
    const cellElements = [];
    for (let i = 0; i < 81; i++) {
        const r = Math.floor(i / 9);
        const c = i % 9;
        const cell = document.createElement('div');
        cell.className = 'sudoku-cell';
        cell.dataset.index = String(i);
        cell.dataset.row = String(r);
        cell.dataset.col = String(c);
        cell.tabIndex = 0;
        cell.setAttribute('role', 'gridcell');

        cell.addEventListener('click', () => {
            selectedIdx = i;
            renderBoard();
        });

        boardEl.appendChild(cell);
        cellElements.push(cell);
    }

    const computePreviewScore = () => {
        const cfg = DIFF_CONFIG[currentDiffKey] || DIFF_CONFIG.easy;
        const basePoints = Math.round(1000 * cfg.multiplier);
        const timeBonus = Math.max(0, Math.round((cfg.targetSec - elapsedSeconds) * 1.5 * cfg.multiplier));
        const penalty = mistakes * 50;
        return Math.max(100, basePoints + timeBonus - penalty);
    };

    const formatClock = (sec) => {
        const m = Math.floor(sec / 60);
        const s = sec % 60;
        return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    };

    const updateHud = () => {
        if (hudScoreEl) hudScoreEl.textContent = String(computePreviewScore());
        if (hudMistakesEl) hudMistakesEl.textContent = String(mistakes);
        if (hudTimerEl) hudTimerEl.textContent = formatClock(elapsedSeconds);
    };

    const startTimer = () => {
        if (timerId) clearInterval(timerId);
        timerId = setInterval(() => {
            if (!isGameOver && !document.hidden) {
                elapsedSeconds++;
                updateHud();
            }
        }, 1000);
    };

    const pushUndoSnapshot = () => {
        undoStack.push({
            userGrid: userGrid.slice(),
            notesMask: new Int32Array(notesMask)
        });
        if (undoStack.length > 60) {
            undoStack.shift();
        }
    };

    const renderCellContent = (cellEl, idx) => {
        cellEl.textContent = '';
        const val = userGrid[idx];
        if (val !== 0) {
            cellEl.textContent = String(val);
            return;
        }

        const nMask = notesMask[idx];
        if (nMask !== 0) {
            const notesWrap = document.createElement('div');
            notesWrap.className = 'sudoku-notes';
            for (let d = 1; d <= 9; d++) {
                const sp = document.createElement('span');
                sp.className = 'sudoku-note-digit';
                sp.textContent = (nMask & (1 << (d - 1))) ? String(d) : '';
                notesWrap.appendChild(sp);
            }
            cellEl.appendChild(notesWrap);
        }
    };

    const renderBoard = () => {
        const selRow = selectedIdx >= 0 ? Math.floor(selectedIdx / 9) : -1;
        const selCol = selectedIdx >= 0 ? selectedIdx % 9 : -1;
        const selBox = selectedIdx >= 0 ? boxIdx(selRow, selCol) : -1;
        const selDigit = selectedIdx >= 0 ? userGrid[selectedIdx] : 0;

        const digitCounts = new Int32Array(10);

        for (let i = 0; i < 81; i++) {
            const el = cellElements[i];
            const r = Math.floor(i / 9);
            const c = i % 9;
            const b = boxIdx(r, c);
            const v = userGrid[i];

            if (v >= 1 && v <= 9 && v === solutionGrid[i]) {
                digitCounts[v]++;
            }

            el.className = 'sudoku-cell';
            if (givenMask[i]) el.classList.add('is-given');
            else if (v !== 0) el.classList.add('is-user');

            if (selectedIdx >= 0) {
                if (i === selectedIdx) {
                    el.classList.add('is-selected');
                } else if (r === selRow || c === selCol || b === selBox) {
                    el.classList.add('is-peer');
                }

                if (selDigit !== 0 && v === selDigit && i !== selectedIdx) {
                    el.classList.add('is-same-digit');
                }
            }

            if ((i === errorCellIndex) || (v !== 0 && !givenMask[i] && v !== solutionGrid[i])) {
                el.classList.add('is-error');
            }

            renderCellContent(el, i);
        }

        // Tuş takımındaki kalan rakam sayaçlarını güncelle
        numButtons.forEach(({ btn, remSpan, digit }) => {
            const rem = Math.max(0, 9 - digitCounts[digit]);
            remSpan.textContent = String(rem);
            btn.classList.toggle('is-completed', rem === 0);
            btn.disabled = isGameOver || (rem === 0 && !isNotesMode);
        });

        updateHud();
    };

    const clearPeerNotes = (idx, placedDigit) => {
        const bit = 1 << (placedDigit - 1);
        const r = Math.floor(idx / 9);
        const c = idx % 9;
        const b = boxIdx(r, c);

        for (let i = 0; i < 81; i++) {
            const rr = Math.floor(i / 9);
            const cc = i % 9;
            if (rr === r || cc === c || boxIdx(rr, cc) === b) {
                notesMask[i] &= ~bit;
            }
        }
    };

    const checkVictory = () => {
        for (let i = 0; i < 81; i++) {
            if (userGrid[i] !== solutionGrid[i]) return false;
        }
        return true;
    };

    const onWinGame = () => {
        isGameOver = true;
        if (timerId) clearInterval(timerId);
        renderBoard();

        // Sunucu anti-cheat minimum süresinin altında bitmişse (test/hızlı çözüm) gerçek süreyi koru,
        // SDK üzerinden zafer modalını aç
        const cfg = DIFF_CONFIG[currentDiffKey] || DIFF_CONFIG.easy;
        if (window.DivanGameSDK && typeof window.DivanGameSDK.openVictoryModal === 'function') {
            window.DivanGameSDK.openVictoryModal({
                difficulty: currentDiffKey,
                difficultyLabel: cfg.label,
                durationSeconds: Math.max(1, elapsedSeconds),
                mistakes,
                completedCells: 81,
                previewScore: computePreviewScore(),
                onPlayAgain: () => startNewGame(currentDiffKey)
            });
        }
    };

    const handleDigitInput = (digit) => {
        if (isGameOver || selectedIdx < 0 || givenMask[selectedIdx]) return;

        // Kalem / Not Modu
        if (isNotesMode) {
            if (userGrid[selectedIdx] !== 0) return;
            pushUndoSnapshot();
            notesMask[selectedIdx] ^= (1 << (digit - 1));
            renderBoard();
            return;
        }

        // Aynı doğru rakam zaten yazılıysa işlem yapma
        if (userGrid[selectedIdx] === digit) return;

        pushUndoSnapshot();
        userGrid[selectedIdx] = digit;
        notesMask[selectedIdx] = 0;

        if (digit !== solutionGrid[selectedIdx]) {
            mistakes++;
            errorCellIndex = selectedIdx;
            setTimeout(() => {
                if (errorCellIndex === selectedIdx) {
                    errorCellIndex = -1;
                    renderBoard();
                }
            }, 450);
            renderBoard();
            return;
        }

        errorCellIndex = -1;
        clearPeerNotes(selectedIdx, digit);
        renderBoard();

        if (checkVictory()) {
            onWinGame();
        }
    };

    const handleErase = () => {
        if (isGameOver || selectedIdx < 0 || givenMask[selectedIdx]) return;
        if (userGrid[selectedIdx] === 0 && notesMask[selectedIdx] === 0) return;

        pushUndoSnapshot();
        userGrid[selectedIdx] = 0;
        notesMask[selectedIdx] = 0;
        errorCellIndex = -1;
        renderBoard();
    };

    const handleUndo = () => {
        if (isGameOver || undoStack.length === 0) return;
        const prev = undoStack.pop();
        userGrid = prev.userGrid;
        notesMask = prev.notesMask;
        errorCellIndex = -1;
        renderBoard();
    };

    const toggleNotesMode = () => {
        isNotesMode = !isNotesMode;
        if (btnNotes) btnNotes.classList.toggle('is-active', isNotesMode);
        if (lblNotesState) lblNotesState.textContent = isNotesMode ? 'AÇIK' : 'KAPALI';
        renderBoard();
    };

    const startNewGame = (diffKey) => {
        currentDiffKey = DIFF_CONFIG[diffKey] ? diffKey : 'easy';
        const cfg = DIFF_CONFIG[currentDiffKey];

        if (diffBar) {
            diffBar.querySelectorAll('[data-difficulty-key]').forEach((b) => {
                b.classList.toggle('active', b.dataset.difficultyKey === currentDiffKey);
            });
        }

        const { puzzle, solution } = generateSudokuPuzzle(cfg.clues);
        solutionGrid = solution;
        userGrid = puzzle.slice();
        givenMask = puzzle.map((v) => v !== 0);
        notesMask = new Int32Array(81);
        undoStack = [];
        mistakes = 0;
        elapsedSeconds = 0;
        isGameOver = false;
        errorCellIndex = -1;

        // İlk boş hücreyi seç
        selectedIdx = userGrid.findIndex((v) => v === 0);
        renderBoard();
        startTimer();

        if (window.DivanGameSDK && typeof window.DivanGameSDK.loadLeaderboard === 'function') {
            window.DivanGameSDK.loadLeaderboard(currentDiffKey);
        }
    };

    // --- Olay Dinleyicileri ---
    if (diffBar) {
        diffBar.addEventListener('click', (e) => {
            const btn = e.target.closest('[data-difficulty-key]');
            if (!btn) return;
            startNewGame(btn.dataset.difficultyKey);
        });
    }

    if (btnNotes) btnNotes.addEventListener('click', toggleNotesMode);
    if (btnUndo) btnUndo.addEventListener('click', handleUndo);
    if (btnErase) btnErase.addEventListener('click', handleErase);
    if (btnNewGame) btnNewGame.addEventListener('click', () => startNewGame(currentDiffKey));

    numButtons.forEach(({ btn, digit }) => {
        btn.addEventListener('click', () => handleDigitInput(digit));
    });

    document.addEventListener('keydown', (e) => {
        // Modal veya input içindeyken klavye kısayollarını devre dışı bırak
        const tag = (e.target && e.target.tagName) ? e.target.tagName.toLowerCase() : '';
        if (tag === 'input' || tag === 'textarea' || tag === 'select') return;
        if (isGameOver) return;

        if (e.key >= '1' && e.key <= '9') {
            e.preventDefault();
            handleDigitInput(Number(e.key));
            return;
        }

        if (e.key === 'Backspace' || e.key === 'Delete' || e.key === '0') {
            e.preventDefault();
            handleErase();
            return;
        }

        if (e.key === 'n' || e.key === 'N' || e.key === 'p' || e.key === 'P') {
            e.preventDefault();
            toggleNotesMode();
            return;
        }

        if ((e.key === 'z' || e.key === 'Z') && (e.ctrlKey || e.metaKey || !e.altKey)) {
            e.preventDefault();
            handleUndo();
            return;
        }

        if (selectedIdx >= 0) {
            let r = Math.floor(selectedIdx / 9);
            let c = selectedIdx % 9;
            let moved = false;

            if (e.key === 'ArrowUp' || e.key === 'w' || e.key === 'W') { r = (r + 8) % 9; moved = true; }
            else if (e.key === 'ArrowDown' || e.key === 's' || e.key === 'S') { r = (r + 1) % 9; moved = true; }
            else if (e.key === 'ArrowLeft' || e.key === 'a' || e.key === 'A') { c = (c + 8) % 9; moved = true; }
            else if (e.key === 'ArrowRight' || e.key === 'd' || e.key === 'D') { c = (c + 1) % 9; moved = true; }

            if (moved) {
                e.preventDefault();
                selectedIdx = r * 9 + c;
                renderBoard();
            }
        }
    });

    // Başlangıç oyununu kur
    const host = document.getElementById('divanGameHost');
    const initialDiff = (host && host.dataset.defaultDifficulty) ? host.dataset.defaultDifficulty : 'easy';
    startNewGame(initialDiff);
});
