/**
 * DİVAN - On-Demand Docker Canlı Demo Başlatıcı & İzleyici Modülü
 * Sıfır Inline Script | Turnstile & CSRF Korumalı
 */

document.addEventListener('DOMContentLoaded', () => {
    const stage = document.getElementById('divanDemoStage');
    if (!stage) return;

    const projectId = stage.dataset.projectId;
    const isAdmin = stage.dataset.isAdmin === 'true';

    const launchBtn = document.getElementById('launchDemoBtn');
    const launcherState = document.getElementById('demoLauncherState');
    const loadingState = document.getElementById('demoLoadingState');
    const busyState = document.getElementById('demoBusyState');
    const busyMessage = document.getElementById('demoBusyMessage');
    const busyCountdown = document.getElementById('demoBusyCountdown');
    const errorBox = document.getElementById('demoErrorBox');
    const iframeContainer = document.getElementById('demoIframeContainer');
    const iframe = document.getElementById('liveDemoIframe');
    const openNewTabBtn = document.getElementById('demoOpenNewTabBtn');
    const reloadBtn = document.getElementById('demoReloadBtn');
    const fullscreenBtn = document.getElementById('demoFullscreenBtn');
    const statusDot = document.getElementById('demoStatusDot');
    const statusText = document.getElementById('demoStatusText');
    const timerBadge = document.getElementById('demoIdleTimer');

    let countdownInterval = null;
    let remainingSec = 0;

    const getCsrfToken = () => {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    };

    const getTurnstileToken = () => {
        const input = stage.querySelector('input[name="cf-turnstile-response"]');
        return input ? input.value : '';
    };

    const formatTime = (totalSeconds) => {
        const s = Math.max(0, Math.floor(totalSeconds));
        const m = Math.floor(s / 60);
        const rem = s % 60;
        return `${String(m).padStart(2, '0')}:${String(rem).padStart(2, '0')}`;
    };

    const stopCountdown = () => {
        if (countdownInterval) {
            clearInterval(countdownInterval);
            countdownInterval = null;
        }
    };

    const startCountdown = (seconds, targetEl, onExpire) => {
        stopCountdown();
        remainingSec = Math.max(0, seconds);
        if (targetEl) targetEl.textContent = formatTime(remainingSec);

        countdownInterval = setInterval(() => {
            remainingSec -= 1;
            if (targetEl) targetEl.textContent = formatTime(remainingSec);
            if (remainingSec <= 0) {
                stopCountdown();
                if (typeof onExpire === 'function') onExpire();
            }
        }, 1000);
    };

    const showError = (msg) => {
        if (!errorBox) return;
        errorBox.textContent = msg;
        errorBox.style.display = 'block';
    };

    const clearError = () => {
        if (!errorBox) return;
        errorBox.textContent = '';
        errorBox.style.display = 'none';
    };

    const normalizeRelativeDemoUrl = (rawUrl) => {
        if (!rawUrl || typeof rawUrl !== 'string') return '';
        const trimmed = rawUrl.trim();
        // Hiçbir zaman 127.0.0.1 / localhost veya harici tam URL kullanma; daima relative /live-demo/... kullan
        if (trimmed.startsWith('/live-demo/')) {
            return trimmed;
        }
        try {
            const parsed = new URL(trimmed, window.location.origin);
            if (parsed.pathname.startsWith('/live-demo/')) {
                return `${parsed.pathname}${parsed.search}${parsed.hash}`;
            }
        } catch {
            // ignore
        }
        return trimmed.startsWith('/') ? trimmed : `/live-demo/${trimmed}`;
    };

    const showRunningDemo = (demoUrl, idleSeconds) => {
        const safeRelativeUrl = normalizeRelativeDemoUrl(demoUrl);
        if (!safeRelativeUrl) return;

        if (launcherState) launcherState.style.display = 'none';
        if (loadingState) loadingState.style.display = 'none';
        if (busyState) busyState.style.display = 'none';
        clearError();

        if (iframeContainer) iframeContainer.style.display = 'block';
        if (iframe && iframe.getAttribute('src') !== safeRelativeUrl) {
            iframe.setAttribute('src', safeRelativeUrl);
        }

        if (openNewTabBtn) {
            openNewTabBtn.setAttribute('href', safeRelativeUrl);
            openNewTabBtn.style.display = 'inline-flex';
        }
        if (reloadBtn) reloadBtn.style.display = 'inline-flex';
        if (fullscreenBtn) fullscreenBtn.style.display = 'inline-flex';

        if (statusDot) {
            statusDot.style.background = '#2ecc71';
            statusDot.style.boxShadow = '0 0 8px #2ecc71';
        }
        if (statusText) statusText.textContent = 'Canlı Container Aktif';

        if (timerBadge) {
            timerBadge.style.display = 'inline-flex';
            const span = timerBadge.querySelector('span');
            startCountdown(idleSeconds || 900, span, () => {
                if (statusText) statusText.textContent = 'Uyku Moduna Geçti';
                if (statusDot) {
                    statusDot.style.background = '#C5A059';
                    statusDot.style.boxShadow = 'none';
                }
            });
        }
    };

    const showBusyState = (message, waitSeconds) => {
        if (launcherState) launcherState.style.display = 'none';
        if (loadingState) loadingState.style.display = 'none';
        if (iframeContainer) iframeContainer.style.display = 'none';
        if (busyState) busyState.style.display = 'flex';
        if (busyMessage) busyMessage.textContent = message;

        if (statusDot) {
            statusDot.style.background = '#F7D688';
            statusDot.style.boxShadow = '0 0 8px #F7D688';
        }
        if (statusText) statusText.textContent = 'Başka Bir Demo Aktif';

        startCountdown(waitSeconds || 60, busyCountdown, () => {
            if (busyState) busyState.style.display = 'none';
            if (launcherState) launcherState.style.display = 'flex';
            if (statusText) statusText.textContent = 'Başlatılmaya Hazır';
        });
    };

    // Sayfa açılışında aktif demo durumunu sorgula
    const checkInitialStatus = async () => {
        try {
            const resp = await fetch(`/api/demos/status/${encodeURIComponent(projectId)}`, {
                method: 'GET',
                headers: { 'Accept': 'application/json' }
            });
            if (!resp.ok) return;
            const data = await resp.json();

            if (data.hasActiveDemo && data.isThisProjectActive && data.demoUrl) {
                showRunningDemo(data.demoUrl, data.remainingSeconds);
            } else if (data.hasActiveDemo && !data.isThisProjectActive) {
                showBusyState(
                    `Şu anda '${data.activeProjectTitle}' demosu aktif. Donanım koruması gereği aynı anda yalnızca 1 demo çalıştırılabilir.`,
                    data.remainingSeconds
                );
            }
        } catch {
            // Sessizce başlatıcı ekranında kal
        }
    };

    if (launchBtn) {
        launchBtn.addEventListener('click', async () => {
            clearError();

            const turnstileToken = getTurnstileToken();
            if (!isAdmin && !turnstileToken) {
                showError('Lütfen önce güvenlik doğrulamasını (Cloudflare Turnstile) tamamlayınız.');
                return;
            }

            if (launcherState) launcherState.style.display = 'none';
            if (busyState) busyState.style.display = 'none';
            if (loadingState) loadingState.style.display = 'flex';
            if (statusText) statusText.textContent = 'Container Başlatılıyor...';

            try {
                const resp = await fetch(`/api/demos/start/${encodeURIComponent(projectId)}`, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json',
                        'Accept': 'application/json',
                        'RequestVerificationToken': getCsrfToken()
                    },
                    body: JSON.stringify({ turnstileToken })
                });

                const data = await resp.json().catch(() => ({}));

                if (resp.ok && data.success && data.demoUrl) {
                    showRunningDemo(data.demoUrl, data.remainingSeconds);
                    return;
                }

                if (data.isBusy) {
                    showBusyState(data.message || 'Şu anda başka bir demo aktif.', data.remainingSeconds);
                    return;
                }

                if (loadingState) loadingState.style.display = 'none';
                if (launcherState) launcherState.style.display = 'flex';
                if (statusText) statusText.textContent = 'Uyku Modunda';
                showError(data.message || 'Canlı demo başlatılırken bir hata oluştu.');

                if (window.turnstile && typeof window.turnstile.reset === 'function') {
                    window.turnstile.reset();
                }
            } catch {
                if (loadingState) loadingState.style.display = 'none';
                if (launcherState) launcherState.style.display = 'flex';
                if (statusText) statusText.textContent = 'Bağlantı Hatası';
                showError('Sunucuya bağlanırken ağ hatası oluştu. Lütfen tekrar deneyin.');
            }
        });
    }

    if (reloadBtn && iframe) {
        reloadBtn.addEventListener('click', () => {
            const currentSrc = iframe.getAttribute('src');
            if (currentSrc) iframe.setAttribute('src', currentSrc);
        });
    }

    if (fullscreenBtn && iframe) {
        fullscreenBtn.addEventListener('click', () => {
            if (iframe.requestFullscreen) {
                iframe.requestFullscreen();
            }
        });
    }

    checkInitialStatus();
});
