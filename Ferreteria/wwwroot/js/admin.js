// Panel de administración: comportamientos sin JavaScript en línea (compatible con la política CSP)
(function () {
    'use strict';
    // Confirmación antes de acciones destructivas: <form data-confirmar="¿Seguro?">
    document.querySelectorAll('form[data-confirmar]').forEach(f =>
        f.addEventListener('submit', e => { if (!confirm(f.dataset.confirmar)) e.preventDefault(); }));
    // Envío automático al cambiar un filtro: <select data-autoenviar>
    document.querySelectorAll('[data-autoenviar]').forEach(el =>
        el.addEventListener('change', () => el.form?.submit()));
})();
