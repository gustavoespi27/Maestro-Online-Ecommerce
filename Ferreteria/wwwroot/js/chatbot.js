// =====================================================================
// Maestro Bot — widget del chatbot de atención al cliente
// Las respuestas se construyen con nodos del DOM (textContent), nunca con innerHTML,
// para evitar inyección de código desde los mensajes.
// =====================================================================
(function () {
    'use strict';

    const ventana = document.getElementById('ventanaChat');
    if (!ventana) return;
    const mensajes = document.getElementById('mensajesChat');
    const sugerencias = document.getElementById('sugerenciasChat');
    const form = document.getElementById('formChat');
    const entrada = document.getElementById('entradaChat');
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const CLAVE_HISTORIAL = 'maestroBotHistorial';
    let historial = [];
    let iniciado = false;

    // ---------- Memoria de la conversación durante la visita (solo en este navegador) ----------
    function guardar() {
        try { sessionStorage.setItem(CLAVE_HISTORIAL, JSON.stringify(historial.slice(-30))); } catch { /* sin almacenamiento */ }
    }
    function cargar() {
        try { return JSON.parse(sessionStorage.getItem(CLAVE_HISTORIAL) || '[]'); } catch { return []; }
    }

    function crear(tag, clase, texto) {
        const el = document.createElement(tag);
        if (clase) el.className = clase;
        if (texto !== undefined) el.textContent = texto;
        return el;
    }

    function enlaceSeguro(url) {
        // Solo rutas internas o WhatsApp
        return /^\/(?!\/)/.test(url) || url.startsWith('https://wa.me/') ? url : '#';
    }

    function pintarMensaje(m, animar = true) {
        const burbuja = crear('div', 'burbuja ' + (m.de === 'usuario' ? 'usuario' : 'bot'));
        if (!animar) burbuja.style.animation = 'none', burbuja.style.opacity = 1, burbuja.style.transform = 'none';
        burbuja.appendChild(crear('div', '', m.texto));

        (m.productos || []).forEach(p => {
            const a = crear('a', 'producto-chat');
            a.href = enlaceSeguro(p.url);
            const img = crear('img');
            img.src = p.imagen || '/img/productos/sin-imagen.svg';
            img.alt = '';
            a.appendChild(img);
            const info = crear('span', '', p.nombre);
            const precio = crear('strong', '', p.precio);
            if (p.precioAnterior) precio.appendChild(crear('s', '', p.precioAnterior));
            info.appendChild(precio);
            a.appendChild(info);
            burbuja.appendChild(a);
        });

        if (m.enlaces && m.enlaces.length) {
            const cont = crear('div', 'enlaces-chat');
            m.enlaces.forEach(e => {
                const a = crear('a', '', e.texto + ' →');
                a.href = enlaceSeguro(e.url);
                if (a.href.startsWith('https://wa.me/')) { a.target = '_blank'; a.rel = 'noopener'; }
                cont.appendChild(a);
            });
            burbuja.appendChild(cont);
        }
        mensajes.appendChild(burbuja);
        mensajes.scrollTop = mensajes.scrollHeight;
    }

    function pintarSugerencias(lista) {
        sugerencias.replaceChildren();
        (lista || []).forEach(s => {
            const b = crear('button', '', s);
            b.type = 'button';
            b.addEventListener('click', () => enviar(s));
            sugerencias.appendChild(b);
        });
    }

    function agregar(m) {
        historial.push(m);
        guardar();
        pintarMensaje(m);
        if (m.de === 'bot') pintarSugerencias(m.sugerencias);
    }

    async function enviar(texto) {
        texto = (texto || '').trim();
        if (!texto) return;
        agregar({ de: 'usuario', texto });
        entrada.value = '';

        const escribiendo = crear('div', 'burbuja bot');
        const puntos = crear('span', 'escribiendo');
        puntos.append(crear('i'), crear('i'), crear('i'));
        escribiendo.appendChild(puntos);
        mensajes.appendChild(escribiendo);
        mensajes.scrollTop = mensajes.scrollHeight;

        try {
            const [resp] = await Promise.all([
                fetch('/Chatbot/Responder', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                    body: JSON.stringify({ texto })
                }),
                new Promise(r => setTimeout(r, 450)) // pequeña pausa para que se sienta natural
            ]);
            if (!resp.ok) throw new Error(resp.status);
            const datos = await resp.json();
            escribiendo.remove();
            agregar({ de: 'bot', texto: datos.texto, enlaces: datos.enlaces, productos: datos.productos, sugerencias: datos.sugerencias });
        } catch {
            escribiendo.remove();
            agregar({
                de: 'bot', texto: 'Uy, tuve un problema para responder. Intenta de nuevo o escríbenos por el formulario de contacto.',
                enlaces: [{ texto: 'Formulario de contacto', url: '/Contacto' }]
            });
        }
    }

    function abrir() {
        document.getElementById('contactoFlotante')?.classList.remove('abierto');
        ventana.classList.add('abierta');
        ventana.setAttribute('aria-hidden', 'false');
        if (!iniciado) {
            iniciado = true;
            historial = cargar();
            if (historial.length) {
                historial.forEach(m => pintarMensaje(m, false));
                pintarSugerencias(historial.filter(m => m.de === 'bot').pop()?.sugerencias);
            } else {
                agregar({
                    de: 'bot',
                    texto: '¡Hola, maestro! 👷 Soy Maestro Bot. Pregúntame por despachos, pagos, devoluciones, el estado de tu pedido o busca un producto (ej: "busco un taladro").',
                    sugerencias: ['¿Cuánto cuesta el despacho?', '¿Cómo sigo mi pedido?', 'Quiero devolver un producto', 'Ver ofertas', 'Busco un taladro']
                });
            }
        }
        setTimeout(() => entrada.focus(), 300);
    }

    function cerrar() {
        ventana.classList.remove('abierta');
        ventana.setAttribute('aria-hidden', 'true');
    }

    document.querySelectorAll('[data-abrir-chat]').forEach(b => b.addEventListener('click', e => { e.preventDefault(); e.stopPropagation(); abrir(); }));
    document.getElementById('cerrarChat').addEventListener('click', cerrar);
    document.addEventListener('keydown', e => { if (e.key === 'Escape' && ventana.classList.contains('abierta')) cerrar(); });
    form.addEventListener('submit', e => { e.preventDefault(); enviar(entrada.value); });
    // Un enlace a ...#maestro-bot abre el chat directamente
    if (location.hash === '#maestro-bot') abrir();
    window.addEventListener('hashchange', () => { if (location.hash === '#maestro-bot') abrir(); });
})();

// ---------- Aviso de cookies técnicas ----------
(function () {
    const banner = document.getElementById('bannerCookies');
    if (!banner) return;
    let aceptado = false;
    try { aceptado = localStorage.getItem('avisoCookies') === '1'; } catch { /* sin almacenamiento */ }
    if (aceptado) return;
    banner.classList.remove('d-none');
    document.getElementById('aceptarCookies').addEventListener('click', () => {
        try { localStorage.setItem('avisoCookies', '1'); } catch { /* sin almacenamiento */ }
        banner.remove();
    });
})();
