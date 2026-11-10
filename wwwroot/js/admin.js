/**
 * Nights - Admin Yönetim Paneli Modülü
 * Onaylama, reddetme, proje silme ve kullanıcı yetki yönetimi
 */
document.addEventListener('DOMContentLoaded', () => {
    // Anı Onaylama / Reddetme
    document.querySelectorAll('[data-admin-action]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const button = e.currentTarget;
            const action = button.dataset.adminAction; // 'approve' | 'reject'
            const id = button.dataset.id;
            const targetUrl = action === 'approve'
                ? '/Admin/AdminMemories/Approve'
                : '/Admin/AdminMemories/Reject';

            const confirmMsg = action === 'approve'
                ? 'Bu anıyı onaylayıp yayına almak istiyor musunuz?'
                : 'Bu anıyı reddedip görseli kalıcı olarak silmek istiyor musunuz?';

            if (!confirm(confirmMsg)) return;

            const formData = new FormData();
            formData.append('id', id);
            formData.append('__RequestVerificationToken', NightsApp.getToken());

            button.disabled = true;

            try {
                const response = await fetch(targetUrl, {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();
                if (result.success) {
                    NightsApp.notify(result.message, false);
                    const card = document.getElementById(`pending-card-${id}`);
                    if (card) {
                        card.style.transition = 'all 0.3s ease';
                        card.style.opacity = '0';
                        card.style.transform = 'scale(0.95)';
                        setTimeout(() => card.remove(), 300);
                    }
                } else {
                    NightsApp.notify(result.message || 'İşlem başarısız.', true);
                    button.disabled = false;
                }
            } catch (err) {
                NightsApp.notify('Sunucuyla iletişim kurulurken hata oluştu.', true);
                button.disabled = false;
            }
        });
    });

    // Proje Silme
    document.querySelectorAll('[data-project-delete]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const button = e.currentTarget;
            const id = button.dataset.id;

            if (!confirm('Bu projeyi ve ilişkili tüm rehber adımlarını silmek istediğinize emin misiniz?')) {
                return;
            }

            const formData = new FormData();
            formData.append('id', id);
            formData.append('__RequestVerificationToken', NightsApp.getToken());

            button.disabled = true;

            try {
                const response = await fetch('/Admin/AdminProjects/Delete', {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();
                if (result.success) {
                    NightsApp.notify('Proje silindi.', false);
                    const row = document.getElementById(`project-row-${id}`);
                    if (row) row.remove();
                } else {
                    NightsApp.notify('Proje silinemedi.', true);
                    button.disabled = false;
                }
            } catch (err) {
                NightsApp.notify('Hata oluştu.', true);
                button.disabled = false;
            }
        });
    });

    // Kullanıcı Not Defteri Yetkisi Açma / Kapatma (Toggle)
    document.querySelectorAll('[data-toggle-note-access]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const button = e.currentTarget;
            const userId = button.dataset.id;

            const formData = new FormData();
            formData.append('userId', userId);
            formData.append('__RequestVerificationToken', NightsApp.getToken());

            button.disabled = true;

            try {
                const response = await fetch('/Admin/AdminUsers/ToggleNoteAccess', {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();
                if (result.success) {
                    NightsApp.notify(result.message, false);
                    const badge = document.getElementById(`access-badge-${userId}`);
                    if (result.hasAccess) {
                        if (badge) badge.innerHTML = '<span style="color: #137333; font-weight: 600; font-size: 0.9rem;">✓ İzin Verildi (280 Kelime)</span>';
                        button.textContent = 'Yetkiyi Kaldır';
                    } else {
                        if (badge) badge.innerHTML = '<span style="color: var(--danger-rose); font-weight: 500; font-size: 0.9rem;">✕ Erişim Yok</span>';
                        button.textContent = 'Yetki Ver';
                    }
                } else {
                    NightsApp.notify(result.message || 'İşlem başarısız.', true);
                }
            } catch {
                NightsApp.notify('Sunucu hatası oluştu.', true);
            } finally {
                button.disabled = false;
            }
        });
    });
});
