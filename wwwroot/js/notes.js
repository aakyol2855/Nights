/**
 * Nights - Özel Not Defteri Modülü
 * Gerçek zamanlı kelime sayacı, not ekleme, düzenleme ve silme
 */
document.addEventListener('DOMContentLoaded', () => {
    const countWords = (text) => {
        if (!text || !text.trim()) return 0;
        return text.trim().split(/\s+/).length;
    };

    // 1. Yeni Not Formu Kelime Sayacı
    const noteContent = document.getElementById('noteContent');
    const wordCountDisplay = document.getElementById('wordCountDisplay');
    const wordCountBadge = document.getElementById('wordCountBadge');

    if (noteContent && wordCountDisplay) {
        noteContent.addEventListener('input', () => {
            const count = countWords(noteContent.value);
            wordCountDisplay.textContent = count;
            if (count > 280) {
                wordCountDisplay.style.color = 'var(--danger-rose)';
                if (wordCountBadge) wordCountBadge.style.borderColor = 'var(--danger-rose)';
            } else {
                wordCountDisplay.style.color = 'var(--accent-sapphire)';
                if (wordCountBadge) wordCountBadge.style.borderColor = 'var(--accent-gold-border)';
            }
        });
    }

    // 2. Yeni Not Kaydetme (AJAX)
    const noteCreateForm = document.getElementById('noteCreateForm');
    if (noteCreateForm) {
        noteCreateForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const title = document.getElementById('noteTitle').value.trim();
            const content = noteContent.value.trim();
            const isPinnedInput = document.getElementById('noteIsPinned');
            const isPinned = isPinnedInput ? isPinnedInput.checked : false;
            const submitBtn = document.getElementById('noteSubmitBtn');

            if (!title) {
                NightsApp.notify('Lütfen not başlığı giriniz.', true);
                return;
            }

            if (!content) {
                NightsApp.notify('Not içeriği boş bırakılamaz.', true);
                return;
            }

            const formData = new FormData();
            formData.append('Title', title);
            formData.append('Content', content);
            formData.append('IsPinned', isPinned);
            formData.append('__RequestVerificationToken', NightsApp.getToken());

            submitBtn.disabled = true;
            submitBtn.innerHTML = '<span>Kaydediliyor...</span>';

            try {
                const response = await fetch('/Notes/Create', {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();
                if (result.success) {
                    NightsApp.notify(result.message, false);
                    setTimeout(() => window.location.reload(), 800);
                } else {
                    NightsApp.notify(result.message || 'Kayıt başarısız.', true);
                    submitBtn.disabled = false;
                    submitBtn.innerHTML = '<span>✦ Notu Kaydet</span>';
                }
            } catch {
                NightsApp.notify('Sunucuyla iletişim kurulurken hata oluştu.', true);
                submitBtn.disabled = false;
                submitBtn.innerHTML = '<span>✦ Notu Kaydet</span>';
            }
        });
    }

    // 3. Not Düzenleme Modalı
    const editModal = document.getElementById('noteEditModal');
    const editNoteId = document.getElementById('editNoteId');
    const editNoteTitle = document.getElementById('editNoteTitle');
    const editNoteContent = document.getElementById('editNoteContent');
    const editNoteIsPinned = document.getElementById('editNoteIsPinned');
    const editWordCountDisplay = document.getElementById('editWordCountDisplay');
    const closeEditModalBtn = document.getElementById('closeEditModalBtn');
    const noteEditForm = document.getElementById('noteEditForm');

    if (editNoteContent && editWordCountDisplay) {
        editNoteContent.addEventListener('input', () => {
            const count = countWords(editNoteContent.value);
            editWordCountDisplay.textContent = count;
            editWordCountDisplay.style.color = count > 280 ? 'var(--danger-rose)' : 'var(--accent-sapphire)';
        });
    }

    document.querySelectorAll('[data-note-edit]').forEach(btn => {
        btn.addEventListener('click', (e) => {
            const button = e.currentTarget;
            editNoteId.value = button.dataset.id;
            editNoteTitle.value = button.dataset.title;
            editNoteContent.value = button.dataset.content;
            if (editNoteIsPinned) {
                editNoteIsPinned.checked = button.dataset.pinned === 'true';
            }
            if (editWordCountDisplay) {
                editWordCountDisplay.textContent = countWords(button.dataset.content);
            }
            if (editModal) editModal.style.display = 'flex';
        });
    });

    if (closeEditModalBtn && editModal) {
        closeEditModalBtn.addEventListener('click', () => {
            editModal.style.display = 'none';
        });
    }

    if (noteEditForm) {
        noteEditForm.addEventListener('submit', async (e) => {
            e.preventDefault();

            const id = editNoteId.value;
            const title = editNoteTitle.value.trim();
            const content = editNoteContent.value.trim();
            const isPinned = editNoteIsPinned ? editNoteIsPinned.checked : false;

            const formData = new FormData();
            formData.append('Id', id);
            formData.append('Title', title);
            formData.append('Content', content);
            formData.append('IsPinned', isPinned);
            formData.append('__RequestVerificationToken', NightsApp.getToken());

            const saveBtn = document.getElementById('saveEditNoteBtn');
            saveBtn.disabled = true;

            try {
                const response = await fetch('/Notes/Edit', {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();
                if (result.success) {
                    NightsApp.notify(result.message, false);
                    setTimeout(() => window.location.reload(), 700);
                } else {
                    NightsApp.notify(result.message || 'Güncelleme başarısız.', true);
                    saveBtn.disabled = false;
                }
            } catch {
                NightsApp.notify('Hata oluştu.', true);
                saveBtn.disabled = false;
            }
        });
    }

    // 4. Not Silme
    document.querySelectorAll('[data-note-delete]').forEach(btn => {
        btn.addEventListener('click', async (e) => {
            const button = e.currentTarget;
            const id = button.dataset.id;

            if (!confirm('Bu notu silmek istediğinize emin misiniz?')) {
                return;
            }

            const formData = new FormData();
            formData.append('id', id);
            formData.append('__RequestVerificationToken', NightsApp.getToken());

            button.disabled = true;

            try {
                const response = await fetch('/Notes/Delete', {
                    method: 'POST',
                    body: formData
                });

                const result = await response.json();
                if (result.success) {
                    NightsApp.notify(result.message, false);
                    const card = document.getElementById(`note-card-${id}`);
                    if (card) {
                        card.style.transition = 'all 0.3s ease';
                        card.style.opacity = '0';
                        card.style.transform = 'scale(0.95)';
                        setTimeout(() => card.remove(), 300);
                    }
                } else {
                    NightsApp.notify(result.message || 'Silme işlemi başarısız.', true);
                    button.disabled = false;
                }
            } catch {
                NightsApp.notify('Sunucu hatası oluştu.', true);
                button.disabled = false;
            }
        });
    });
});
