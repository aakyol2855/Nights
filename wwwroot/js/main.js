/**
 * Nights - Çekirdek JavaScript Modülü
 * Sıfır Inline Script Mimarisi & Global Yardımcılar
 */
const NightsApp = (() => {
    let toastTimer = null;

    const getCsrfToken = () => {
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    };

    const showToast = (message, isError = false) => {
        let toast = document.getElementById('app-toast');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'app-toast';
            document.body.appendChild(toast);
        }

        clearTimeout(toastTimer);
        toast.textContent = message;
        toast.style.background = isError ? 'var(--danger-rose)' : 'var(--accent-sapphire)';
        toast.style.display = 'block';

        toastTimer = setTimeout(() => {
            toast.style.display = 'none';
        }, 4500);
    };

    const initMobileMenu = () => {
        const toggleBtn = document.querySelector('[data-nav-toggle]');
        const navMenu = document.querySelector('.nav-links');
        if (toggleBtn && navMenu) {
            toggleBtn.addEventListener('click', () => {
                navMenu.classList.toggle('open');
            });
        }
    };

    // DOM hazır olduğunda genel etkileşimleri bağla
    document.addEventListener('DOMContentLoaded', () => {
        initMobileMenu();
    });

    return {
        getToken: getCsrfToken,
        notify: showToast
    };
})();
