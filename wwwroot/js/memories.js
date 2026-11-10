/**
 * Nights - Anılar Modülü (Memories Module)
 * Dosya yükleme, progress ve kart etkileşimleri
 */
document.addEventListener('DOMContentLoaded', () => {
    const memoryForm = document.getElementById('memoryUploadForm');
    if (!memoryForm) return;

    const fileInput = document.getElementById('memoryFileInput');
    const descInput = document.getElementById('memoryDescInput');
    const submitBtn = document.getElementById('memorySubmitBtn');

    memoryForm.addEventListener('submit', async (e) => {
        e.preventDefault();

        if (!fileInput.files.length) {
            NightsApp.notify('Lütfen bir görsel seçiniz.', true);
            return;
        }

        const file = fileInput.files[0];
        if (file.size > 5 * 1024 * 1024) {
            NightsApp.notify('Görsel boyutu en fazla 5MB olabilir.', true);
            return;
        }

        const desc = descInput.value.trim();
        if (desc.length < 3) {
            NightsApp.notify('Lütfen anınız için en az 3 karakterlik bir açıklama yazınız.', true);
            return;
        }

        const formData = new FormData();
        formData.append('Description', desc);
        formData.append('File', file);
        formData.append('__RequestVerificationToken', NightsApp.getToken());

        const originalBtnText = submitBtn.innerHTML;
        submitBtn.disabled = true;
        submitBtn.innerHTML = '<span>İşleniyor & Yükleniyor...</span>';

        try {
            const response = await fetch('/Memories/Submit', {
                method: 'POST',
                body: formData
            });

            if (response.status === 429) {
                NightsApp.notify('Çok fazla yükleme isteği gönderdiniz. Lütfen 1 dakika bekleyiniz.', true);
                return;
            }

            const data = await response.json();
            if (data.success) {
                NightsApp.notify(data.message, false);
                memoryForm.reset();
            } else {
                NightsApp.notify(data.message || 'Yükleme sırasında hata oluştu.', true);
            }
        } catch (error) {
            NightsApp.notify('Bağlantı hatası veya dosya boyutu sınırı aşıldı.', true);
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = originalBtnText;
        }
    });
});
