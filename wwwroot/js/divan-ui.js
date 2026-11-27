/**
 * DİVAN - Arayüz, Tema ve Etkileşim Modülü
 * Sıfır Inline Script & Modüler Yapı
 */

document.addEventListener('DOMContentLoaded', () => {
    // 1. Tema Yönetimi (Karanlık / Aydınlık Mod Değişimi)
    const initThemeToggle = () => {
        const toggleButtons = document.querySelectorAll('#themeToggleBtn, .divan-theme-toggle');
        if (!toggleButtons.length) return;

        // Mevcut temayı oku veya varsayılan koyu temayı uygula
        const currentTheme = document.documentElement.getAttribute('data-theme') || localStorage.getItem('divan-theme') || 'dark';
        document.documentElement.setAttribute('data-theme', currentTheme);

        toggleButtons.forEach(btn => {
            btn.addEventListener('click', (e) => {
                e.preventDefault();
                const activeTheme = document.documentElement.getAttribute('data-theme') || 'dark';
                const nextTheme = activeTheme === 'dark' ? 'light' : 'dark';

                document.documentElement.setAttribute('data-theme', nextTheme);
                localStorage.setItem('divan-theme', nextTheme);
            });
        });
    };

    // 2. Kullanıcı Profil Menüsü Dropdown
    const initUserDropdown = () => {
        const userBtn = document.getElementById('userMenuToggleBtn');
        const userDropdown = document.getElementById('userMenuDropdown');

        if (userBtn && userDropdown) {
            userBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                userDropdown.classList.toggle('show');
            });

            // Sayfa dışına tıklandığında menüyü kapat
            document.addEventListener('click', (e) => {
                if (!userDropdown.contains(e.target) && e.target !== userBtn) {
                    userDropdown.classList.remove('show');
                }
            });

            // ESC tuşuna basıldığında kapat
            document.addEventListener('keydown', (e) => {
                if (e.key === 'Escape') {
                    userDropdown.classList.remove('show');
                }
            });
        }
    };

    // 3. Mobil Menü Aç/Kapat
    const initMobileMenu = () => {
        const mobileBtn = document.querySelector('[data-divan-toggle]');
        const menu = document.querySelector('.divan-menu');
        if (mobileBtn && menu) {
            mobileBtn.addEventListener('click', () => {
                menu.classList.toggle('show');
            });
        }
    };

    // 4. Saf SVG Sparkline Çizici Fonksiyon (Gerekirse)
    const renderSparkline = (svgElement, dataPoints) => {
        if (!svgElement || !dataPoints || dataPoints.length < 2) return;

        const width = 120;
        const height = 22;
        const min = 0;
        const max = 100;
        const step = width / (dataPoints.length - 1);

        let pathD = '';
        dataPoints.forEach((val, index) => {
            const x = index * step;
            const y = height - (Math.max(min, Math.min(max, val)) / (max - min)) * (height - 4) - 2;
            pathD += (index === 0 ? `M ${x.toFixed(1)} ${y.toFixed(1)}` : ` L ${x.toFixed(1)} ${y.toFixed(1)}`);
        });

        svgElement.innerHTML = `
            <path d="${pathD}" fill="none" stroke="#2ECC71" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
            <circle cx="${width}" cy="${height - (dataPoints[dataPoints.length - 1] / 100) * (height - 4) - 2}" r="2" fill="#2ECC71"/>
        `;
    };

    const initSparklines = () => {
        const cpuSvg = document.getElementById('sparkline-cpu');
        const ramSvg = document.getElementById('sparkline-ram');
        const diskSvg = document.getElementById('sparkline-disk');
        const dataHolder = document.getElementById('divan-stats-data');

        if (!cpuSvg && !ramSvg && !diskSvg) return;

        let cpuData = [10, 18, 14, 22, 12, 16];
        let ramData = [32, 33, 35, 34, 34, 34];
        let diskData = [62, 62, 62, 62, 62, 62];

        if (dataHolder) {
            if (dataHolder.dataset.cpu) cpuData = dataHolder.dataset.cpu.split(',').map(Number);
            if (dataHolder.dataset.ram) ramData = dataHolder.dataset.ram.split(',').map(Number);
            if (dataHolder.dataset.disk) diskData = dataHolder.dataset.disk.split(',').map(Number);
        }

        if (cpuSvg) renderSparkline(cpuSvg, cpuData);
        if (ramSvg) renderSparkline(ramSvg, ramData);
        if (diskSvg) renderSparkline(diskSvg, diskData);
    };

    // Tüm modülleri başlat
    initThemeToggle();
    initUserDropdown();
    initMobileMenu();
    initSparklines();

    // 5. Parola Göster / Gizle Butonları (Giriş & Kayıt Ekranı)
    const initPasswordToggles = () => {
        const toggleBtns = document.querySelectorAll('[data-toggle-password]');
        toggleBtns.forEach(btn => {
            btn.addEventListener('click', () => {
                const targetId = btn.getAttribute('data-toggle-password');
                const input = document.getElementById(targetId);
                if (!input) return;

                const isPassword = input.getAttribute('type') === 'password';
                input.setAttribute('type', isPassword ? 'text' : 'password');
                btn.classList.toggle('active', isPassword);
            });
        });
    };
    initPasswordToggles();
});
