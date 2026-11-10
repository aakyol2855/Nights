/**
 * Nights - Projeler ve Canlı Demo Gösterim Modülü
 */
document.addEventListener('DOMContentLoaded', () => {
    // Demo ekranı yenile butonu
    const reloadBtn = document.querySelector('[data-demo-reload]');
    if (reloadBtn) {
        reloadBtn.addEventListener('click', () => {
            const iframe = document.querySelector('.demo-iframe');
            if (iframe) {
                const currentSrc = iframe.src;
                iframe.src = '';
                iframe.src = currentSrc;
                NightsApp.notify('Canlı ekran yeniden yükleniyor...', false);
            }
        });
    }

    // Tam ekran geçiş butonu
    const fullscreenBtn = document.querySelector('[data-demo-fullscreen]');
    if (fullscreenBtn) {
        fullscreenBtn.addEventListener('click', () => {
            const iframe = document.querySelector('.demo-iframe');
            if (iframe) {
                if (iframe.requestFullscreen) {
                    iframe.requestFullscreen();
                } else if (iframe.webkitRequestFullscreen) {
                    iframe.webkitRequestFullscreen();
                }
            }
        });
    }
});
