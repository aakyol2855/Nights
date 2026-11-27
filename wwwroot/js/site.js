/**
 * DİVAN - Genel Ziyaretçi Arayüzü & Çok Dilli (TR/EN) Geçiş Betiği
 * Sıfır Inline Script | CSP & CSRF Uyumlu
 */
document.addEventListener('DOMContentLoaded', () => {
    const langBtn = document.getElementById('divanLangToggleBtn');
    const langForm = document.getElementById('divanLangForm');

    if (langBtn && langForm) {
        langBtn.addEventListener('click', () => {
            langBtn.setAttribute('aria-busy', 'true');
            langForm.submit();
        });
    }
});
