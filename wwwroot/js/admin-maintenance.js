/**
 * DİVAN - Çöp Kutusu, Sistem Günlüğü ve Veri Bakım Merkezi Betiği
 * Sıfır Inline Script | Anti-Forgery (CSRF) Korumalı
 */
document.addEventListener('DOMContentLoaded', () => {
    const getCsrfToken = () => {
        const input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    };

    const banner = document.getElementById('adminActionBanner');
    const showBanner = (message, isSuccess) => {
        if (!banner) return;
        banner.textContent = message;
        banner.style.display = 'block';
        if (isSuccess) {
            banner.style.background = 'rgba(46, 204, 113, 0.14)';
            banner.style.border = '1px solid #2ecc71';
            banner.style.color = '#2ecc71';
        } else {
            banner.style.background = 'rgba(231, 76, 60, 0.14)';
            banner.style.border = '1px solid var(--danger-rose)';
            banner.style.color = '#ffb8b8';
        }
        banner.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    };

    // Merkezi Onay Modalı
    const modal = document.getElementById('divanConfirmModal');
    const modalTitle = document.getElementById('divanConfirmTitle');
    const modalBody = document.getElementById('divanConfirmBody');
    const modalCancel = document.getElementById('divanConfirmCancel');
    const modalAccept = document.getElementById('divanConfirmAccept');

    let pendingAction = null;

    const closeModal = () => {
        if (modal) modal.style.display = 'none';
        pendingAction = null;
    };

    const openConfirmModal = (title, body, onConfirm) => {
        if (!modal || !modalTitle || !modalBody) {
            if (window.confirm(`${title}\n\n${body}`)) {
                onConfirm();
            }
            return;
        }
        modalTitle.textContent = title;
        modalBody.textContent = body;
        pendingAction = onConfirm;
        modal.style.display = 'flex';
    };

    if (modalCancel) {
        modalCancel.addEventListener('click', closeModal);
    }
    if (modal) {
        modal.addEventListener('click', (e) => {
            if (e.target === modal) closeModal();
        });
    }
    if (modalAccept) {
        modalAccept.addEventListener('click', async () => {
            const action = pendingAction;
            closeModal();
            if (typeof action === 'function') {
                await action();
            }
        });
    }

    const postFormAction = async (url, formFields) => {
        const body = new URLSearchParams();
        if (formFields) {
            Object.entries(formFields).forEach(([k, v]) => body.append(k, String(v)));
        }

        const resp = await fetch(url, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded; charset=UTF-8',
                'Accept': 'application/json',
                'RequestVerificationToken': getCsrfToken()
            },
            body: body.toString()
        });

        const data = await resp.json().catch(() => ({ success: false, message: 'Beklenmeyen sunucu yanıtı.' }));
        return { ok: resp.ok && data.success, message: data.message || (resp.ok ? 'İşlem tamamlandı.' : 'İşlem başarısız oldu.') };
    };

    // 1. Çöp Kutusu: Geri Getir (Restore)
    document.querySelectorAll('[data-trash-restore]').forEach((btn) => {
        btn.addEventListener('click', () => {
            const url = btn.dataset.url;
            const entityType = btn.dataset.entityType;
            const entityId = btn.dataset.entityId;
            const title = btn.dataset.itemTitle || `#${entityId}`;

            openConfirmModal(
                'Kaydı Geri Getir',
                `'${title}' kaydı çöp kutusundan çıkarılarak yayına / aktif listeye geri alınacaktır. Onaylıyor musunuz?`,
                async () => {
                    btn.disabled = true;
                    try {
                        const res = await postFormAction(url, { entityType, id: entityId });
                        showBanner(res.message, res.ok);
                        if (res.ok) {
                            const row = document.querySelector(`[data-trash-row="${entityType}-${entityId}"]`);
                            if (row) row.remove();
                        } else {
                            btn.disabled = false;
                        }
                    } catch {
                        showBanner('Sunucuya bağlanırken hata oluştu.', false);
                        btn.disabled = false;
                    }
                }
            );
        });
    });

    // 2. Çöp Kutusu: Kalıcı Sil (Hard Delete)
    document.querySelectorAll('[data-trash-hard-delete]').forEach((btn) => {
        btn.addEventListener('click', () => {
            const url = btn.dataset.url;
            const entityType = btn.dataset.entityType;
            const entityId = btn.dataset.entityId;
            const title = btn.dataset.itemTitle || `#${entityId}`;

            openConfirmModal(
                'Kalıcı Olarak Sil',
                `'${title}' kaydı ve varsa ilişkili fiziksel dosyaları GERİ DÖNÜŞÜ OLMAKSIZIN silinecektir. Emin misiniz?`,
                async () => {
                    btn.disabled = true;
                    try {
                        const res = await postFormAction(url, { entityType, id: entityId });
                        showBanner(res.message, res.ok);
                        if (res.ok) {
                            const row = document.querySelector(`[data-trash-row="${entityType}-${entityId}"]`);
                            if (row) row.remove();
                        } else {
                            btn.disabled = false;
                        }
                    } catch {
                        showBanner('Sunucuya bağlanırken hata oluştu.', false);
                        btn.disabled = false;
                    }
                }
            );
        });
    });

    // 3. Veri Bakım & Teşhis Tek-Tık Aksiyonları
    document.querySelectorAll('[data-maintenance-action]').forEach((btn) => {
        btn.addEventListener('click', () => {
            const url = btn.dataset.actionUrl;
            const title = btn.dataset.confirmTitle || 'Bakım İşlemi';
            const body = btn.dataset.confirmBody || 'Bu bakım işlemini çalıştırmak istiyor musunuz?';

            openConfirmModal(title, body, async () => {
                const originalText = btn.textContent;
                btn.disabled = true;
                btn.textContent = 'İşleniyor...';
                try {
                    const res = await postFormAction(url, {});
                    showBanner(res.message, res.ok);
                    if (res.ok) {
                        setTimeout(() => window.location.reload(), 1200);
                    } else {
                        btn.disabled = false;
                        btn.textContent = originalText;
                    }
                } catch {
                    showBanner('Bakım isteği sırasında ağ hatası oluştu.', false);
                    btn.disabled = false;
                    btn.textContent = originalText;
                }
            });
        });
    });

    // 4. Sistem Günlüğü Filtre Seçim Değişikliklerinde Hızlı Gönderim
    const logFilterForm = document.getElementById('auditLogFilterForm');
    if (logFilterForm) {
        const autoSelects = logFilterForm.querySelectorAll('#filterCategory, #filterLevel');
        autoSelects.forEach((sel) => {
            sel.addEventListener('change', () => logFilterForm.submit());
        });
    }
});
