/**
 * DİVAN OYUNHANE - Ortak Oyun Bazlı Top-List & Skor Gönderim SDK'sı (window.DivanGameSDK)
 * Yeni eklenen her IGameModule bu merkezi modülü doğrudan kullanabilir.
 */

(() => {
    const formatDuration = (sec) => {
        const s = Math.max(0, Math.floor(sec || 0));
        const m = Math.floor(s / 60);
        const r = s % 60;
        return `${String(m).padStart(2, '0')}:${String(r).padStart(2, '0')}`;
    };

    const getCsrfToken = () => {
        const el = document.querySelector('input[name="__RequestVerificationToken"]');
        return el ? el.value : '';
    };

    document.addEventListener('DOMContentLoaded', () => {
        const host = document.getElementById('divanGameHost');
        if (!host) return;

        const gameSlug = host.dataset.gameSlug || '';
        const isAuthenticated = host.dataset.isAuthenticated === 'true';
        const lbContainer = document.getElementById('gameLeaderboardContainer');
        const lbTabsWrap = document.getElementById('leaderboardDiffTabs');

        const modal = document.getElementById('gameVictoryModal');
        const summaryText = document.getElementById('victorySummaryText');
        const modalScore = document.getElementById('modalFinalScore');
        const modalTime = document.getElementById('modalFinalTime');
        const modalMistakes = document.getElementById('modalFinalMistakes');
        const guestInput = document.getElementById('guestNicknameInput');
        const feedbackBox = document.getElementById('victoryServerFeedback');
        const submitBtn = document.getElementById('submitScoreModalBtn');
        const playAgainBtn = document.getElementById('playAgainModalBtn');

        let activeLbDiff = host.dataset.defaultDifficulty || 'all';
        let pendingSubmission = null;
        let onPlayAgainCallback = null;
        let scoreSubmittedForRound = false;

        // Misafir son takma adını sessionStorage'dan hatırla
        if (guestInput) {
            const savedNick = sessionStorage.getItem('divan_guest_nick');
            if (savedNick) guestInput.value = savedNick;
        }

        const renderLeaderboardEntries = (entries) => {
            if (!lbContainer) return;
            lbContainer.textContent = '';

            if (!Array.isArray(entries) || entries.length === 0) {
                const empty = document.createElement('div');
                empty.style.cssText = 'padding: 1.85rem 1rem; text-align: center; color: var(--text-muted); font-size: 0.9rem;';
                empty.textContent = 'Bu zorluk seviyesinde henüz kayıtlı bir derece yok. İlk rekoru siz kırın!';
                lbContainer.appendChild(empty);
                return;
            }

            entries.forEach((item) => {
                const row = document.createElement('div');
                row.className = 'tier-row';
                if (item.rank === 1) row.classList.add('is-top-1');
                else if (item.rank === 2) row.classList.add('is-top-2');
                else if (item.rank === 3) row.classList.add('is-top-3');
                if (item.isCurrentUser) row.classList.add('is-current-player');

                const badge = document.createElement('span');
                badge.className = 'tier-rank-badge';
                badge.textContent = String(item.rank);

                const meta = document.createElement('div');
                meta.className = 'tier-player-meta';

                const nameRow = document.createElement('div');
                nameRow.className = 'tier-player-name';
                const nameSpan = document.createElement('span');
                nameSpan.textContent = item.playerDisplayName || 'Oyuncu';
                nameRow.appendChild(nameSpan);

                if (item.isGuestSession) {
                    const guestPill = document.createElement('span');
                    guestPill.className = 'guest-pill';
                    guestPill.textContent = 'Misafir';
                    nameRow.appendChild(guestPill);
                }

                const subRow = document.createElement('div');
                subRow.className = 'tier-player-sub';
                subRow.textContent = `${item.difficultyLabel} • Süre: ${formatDuration(item.durationSeconds)} • Hata: ${item.mistakes}`;

                meta.appendChild(nameRow);
                meta.appendChild(subRow);

                const ptsBox = document.createElement('div');
                ptsBox.className = 'tier-points-box';
                const ptsVal = document.createElement('div');
                ptsVal.className = 'tier-points-val';
                ptsVal.textContent = String(item.score);
                const ptsLbl = document.createElement('div');
                ptsLbl.className = 'tier-points-lbl';
                ptsLbl.textContent = `+${item.tierPoints} Tier`;
                ptsBox.appendChild(ptsVal);
                ptsBox.appendChild(ptsLbl);

                row.appendChild(badge);
                row.appendChild(meta);
                row.appendChild(ptsBox);
                lbContainer.appendChild(row);
            });
        };

        const syncActiveTabUi = (diffKey) => {
            activeLbDiff = diffKey;
            if (!lbTabsWrap) return;
            lbTabsWrap.querySelectorAll('[data-lb-diff]').forEach((btn) => {
                btn.classList.toggle('active', btn.dataset.lbDiff === diffKey);
            });
        };

        const loadLeaderboard = async (diffKey) => {
            const targetDiff = diffKey || activeLbDiff || 'all';
            syncActiveTabUi(targetDiff);

            try {
                const resp = await fetch(`/api/games/${encodeURIComponent(gameSlug)}/leaderboard?difficulty=${encodeURIComponent(targetDiff)}`, {
                    method: 'GET',
                    headers: { 'Accept': 'application/json' }
                });
                if (!resp.ok) return;
                const data = await resp.json();
                if (data.success) {
                    renderLeaderboardEntries(data.entries);
                }
            } catch {
                // Ağ kesintisinde mevcut listeyi koru
            }
        };

        if (lbTabsWrap) {
            lbTabsWrap.addEventListener('click', (e) => {
                const btn = e.target.closest('[data-lb-diff]');
                if (!btn) return;
                loadLeaderboard(btn.dataset.lbDiff);
            });
        }

        const submitCurrentScore = async () => {
            if (!pendingSubmission || scoreSubmittedForRound) return;

            const displayName = guestInput ? guestInput.value.trim() : '';
            if (!isAuthenticated && displayName) {
                sessionStorage.setItem('divan_guest_nick', displayName);
            }

            if (submitBtn) submitBtn.disabled = true;

            try {
                const payload = {
                    difficulty: pendingSubmission.difficulty,
                    score: pendingSubmission.previewScore || 0,
                    durationSeconds: pendingSubmission.durationSeconds,
                    mistakes: pendingSubmission.mistakes,
                    completedCells: pendingSubmission.completedCells,
                    displayName
                };

                const resp = await fetch(`/api/games/${encodeURIComponent(gameSlug)}/score`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json',
                        'RequestVerificationToken': getCsrfToken()
                    },
                    body: JSON.stringify(payload)
                });

                const data = await resp.json().catch(() => ({}));

                if (resp.ok && data.success) {
                    scoreSubmittedForRound = true;
                    if (modalScore && data.finalScore) {
                        modalScore.textContent = String(data.finalScore);
                    }
                    if (feedbackBox) {
                        const rankInfo = data.currentRank
                            ? ` Sıralamanız: #${data.currentRank} (+${data.earnedTierPoints} Tier Puanı).`
                            : '';
                        feedbackBox.textContent = `${data.message}${rankInfo}`;
                        feedbackBox.style.display = 'block';
                    }
                    if (submitBtn) {
                        submitBtn.style.display = 'none';
                    }

                    syncActiveTabUi(pendingSubmission.difficulty);
                    if (Array.isArray(data.leaderboard)) {
                        renderLeaderboardEntries(data.leaderboard);
                    } else {
                        await loadLeaderboard(pendingSubmission.difficulty);
                    }
                } else if (feedbackBox) {
                    feedbackBox.textContent = data.message || 'Skor gönderilirken bir hata oluştu.';
                    feedbackBox.style.display = 'block';
                    if (submitBtn) submitBtn.disabled = false;
                }
            } catch {
                if (feedbackBox) {
                    feedbackBox.textContent = 'Sunucuya ulaşılamadı. Lütfen tekrar deneyin.';
                    feedbackBox.style.display = 'block';
                }
                if (submitBtn) submitBtn.disabled = false;
            }
        };

        if (submitBtn) {
            submitBtn.addEventListener('click', submitCurrentScore);
        }

        if (playAgainBtn) {
            playAgainBtn.addEventListener('click', () => {
                if (modal) modal.classList.remove('show');
                if (typeof onPlayAgainCallback === 'function') {
                    onPlayAgainCallback();
                }
            });
        }

        window.DivanGameSDK = {
            formatDuration,
            loadLeaderboard,
            openVictoryModal: (opts) => {
                pendingSubmission = opts;
                onPlayAgainCallback = opts.onPlayAgain || null;
                scoreSubmittedForRound = false;

                if (summaryText) {
                    summaryText.textContent = `${opts.difficultyLabel || opts.difficulty} seviyesini başarıyla tamamladınız.`;
                }
                if (modalScore) modalScore.textContent = String(opts.previewScore || 0);
                if (modalTime) modalTime.textContent = formatDuration(opts.durationSeconds);
                if (modalMistakes) modalMistakes.textContent = String(opts.mistakes || 0);

                if (feedbackBox) {
                    feedbackBox.style.display = 'none';
                    feedbackBox.textContent = '';
                }
                if (submitBtn) {
                    submitBtn.style.display = 'inline-flex';
                    submitBtn.disabled = false;
                }

                if (modal) modal.classList.add('show');

                // Giriş yapmış kullanıcıda takma ad sorulmadığı için skoru otomatik olarak hemen kaydet!
                if (isAuthenticated) {
                    submitCurrentScore();
                } else if (guestInput) {
                    setTimeout(() => guestInput.focus(), 80);
                }
            }
        };
    });
})();
