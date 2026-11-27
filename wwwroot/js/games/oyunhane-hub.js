/**
 * DİVAN OYUNHANE - Ana Vitrin & Genel Top Tier List Etkileşim Modülü
 * Sıfır Inline Script | XSS Korumalı DOM Oluşturucu
 */

document.addEventListener('DOMContentLoaded', () => {
    const refreshBtn = document.getElementById('refreshTopTierBtn');
    const container = document.getElementById('globalTopTierContainer');
    if (!refreshBtn || !container) return;

    const renderGlobalTier = (entries) => {
        container.textContent = '';

        if (!Array.isArray(entries) || entries.length === 0) {
            const empty = document.createElement('div');
            empty.style.cssText = 'padding: 2rem 1rem; text-align: center; color: var(--text-muted); font-size: 0.9rem;';
            empty.textContent = 'Henüz sıralamaya giren bir oyuncu bulunmuyor. İlk oyunu tamamlayarak zirvenin ilk sahibi olun!';
            container.appendChild(empty);
            return;
        }

        entries.forEach((entry) => {
            const row = document.createElement('div');
            row.className = 'tier-row';
            if (entry.rank === 1) row.classList.add('is-top-1');
            else if (entry.rank === 2) row.classList.add('is-top-2');
            else if (entry.rank === 3) row.classList.add('is-top-3');
            if (entry.isCurrentUser) row.classList.add('is-current-player');

            const badge = document.createElement('span');
            badge.className = 'tier-rank-badge';
            badge.textContent = String(entry.rank);

            const meta = document.createElement('div');
            meta.className = 'tier-player-meta';

            const nameRow = document.createElement('div');
            nameRow.className = 'tier-player-name';
            const nameSpan = document.createElement('span');
            nameSpan.textContent = entry.playerDisplayName || 'Oyuncu';
            nameRow.appendChild(nameSpan);

            if (entry.isGuestSession) {
                const guestPill = document.createElement('span');
                guestPill.className = 'guest-pill';
                guestPill.textContent = 'Misafir';
                nameRow.appendChild(guestPill);
            }

            const subRow = document.createElement('div');
            subRow.className = 'tier-player-sub';
            subRow.appendChild(document.createTextNode('Ünvan: '));
            const titleStrong = document.createElement('strong');
            titleStrong.style.color = 'var(--gold-bright)';
            titleStrong.textContent = entry.tierTitle || 'Ehliyetli';
            subRow.appendChild(titleStrong);
            subRow.appendChild(document.createTextNode(` • ${entry.gamesRankedCount} Oyun Derecesi`));

            meta.appendChild(nameRow);
            meta.appendChild(subRow);

            const ptsBox = document.createElement('div');
            ptsBox.className = 'tier-points-box';
            const ptsVal = document.createElement('div');
            ptsVal.className = 'tier-points-val';
            ptsVal.textContent = String(entry.totalTierPoints);
            const ptsLbl = document.createElement('div');
            ptsLbl.className = 'tier-points-lbl';
            ptsLbl.textContent = 'Tier Puanı';
            ptsBox.appendChild(ptsVal);
            ptsBox.appendChild(ptsLbl);

            row.appendChild(badge);
            row.appendChild(meta);
            row.appendChild(ptsBox);
            container.appendChild(row);
        });
    };

    refreshBtn.addEventListener('click', async () => {
        refreshBtn.disabled = true;
        try {
            const resp = await fetch('/api/games/top-tier', {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (resp.ok) {
                const data = await resp.json();
                if (data.success) {
                    renderGlobalTier(data.entries);
                }
            }
        } finally {
            refreshBtn.disabled = false;
        }
    });
});
