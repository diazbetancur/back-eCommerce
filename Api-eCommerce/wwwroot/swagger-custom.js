// ========================================
// Swagger UI - JavaScript Personalizado
// eCommerce Multi-Tenant API
// ========================================

(function() {
    'use strict';

    console.log('?? eCommerce Multi-Tenant API - Swagger UI Loaded');

    // ========================================
    // Auto-completar headers comunes
    // ========================================
    function setupHeaderAutocomplete() {
        // Detectar cuando se expande un endpoint
        const observer = new MutationObserver(function(mutations) {
            mutations.forEach(function(mutation) {
                if (mutation.addedNodes.length) {
                    // Buscar inputs de headers
                    const tenantInputs = document.querySelectorAll('input[placeholder*="X-Tenant-Slug"]');
                    const sessionInputs = document.querySelectorAll('input[placeholder*="X-Session-Id"]');

                    // Auto-completar X-Tenant-Slug desde localStorage
                    tenantInputs.forEach(input => {
                        const savedTenant = localStorage.getItem('swagger_tenant_slug');
                        if (savedTenant && !input.value) {
                            input.value = savedTenant;
                            input.style.borderColor = '#10b981';
                        }

                        // Guardar en localStorage cuando cambie
                        input.addEventListener('change', function() {
                            localStorage.setItem('swagger_tenant_slug', this.value);
                        });
                    });

                    // Auto-completar X-Session-Id
                    sessionInputs.forEach(input => {
                        let savedSession = localStorage.getItem('swagger_session_id');
                        
                        // Generar session ID si no existe
                        if (!savedSession) {
                            savedSession = 'sess_' + generateUUID();
                            localStorage.setItem('swagger_session_id', savedSession);
                        }

                        if (!input.value) {
                            input.value = savedSession;
                            input.style.borderColor = '#10b981';
                        }
                    });
                }
            });
        });

        // Observar cambios en el DOM
        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    // ========================================
    // Generar UUID v4
    // ========================================
    function generateUUID() {
        return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
            const r = Math.random() * 16 | 0;
            const v = c === 'x' ? r : (r & 0x3 | 0x8);
            return v.toString(16);
        });
    }

    // ========================================
    // Agregar botón de "Clear Session"
    // ========================================
    function addClearSessionButton() {
        setTimeout(() => {
            const topbar = document.querySelector('.swagger-ui .topbar-wrapper');
            if (topbar && !document.getElementById('clear-session-btn')) {
                const btn = document.createElement('button');
                btn.id = 'clear-session-btn';
                btn.innerHTML = '??? Clear Session';
                btn.style.cssText = `
                    background-color: #ef4444;
                    color: white;
                    border: none;
                    padding: 8px 16px;
                    border-radius: 6px;
                    cursor: pointer;
                    font-weight: 600;
                    margin-left: 10px;
                    transition: all 0.2s ease;
                `;
                
                btn.addEventListener('mouseover', function() {
                    this.style.backgroundColor = '#dc2626';
                    this.style.transform = 'translateY(-2px)';
                });
                
                btn.addEventListener('mouseout', function() {
                    this.style.backgroundColor = '#ef4444';
                    this.style.transform = 'translateY(0)';
                });
                
                btn.addEventListener('click', function() {
                    if (confirm('¿Limpiar tenant slug y session ID guardados?')) {
                        localStorage.removeItem('swagger_tenant_slug');
                        localStorage.removeItem('swagger_session_id');
                        alert('? Session cleared. Refresh the page.');
                        location.reload();
                    }
                });
                
                topbar.appendChild(btn);
            }
        }, 1000);
    }

    // ========================================
    // Mostrar tenant y session actuales
    // ========================================
    function showCurrentSession() {
        setTimeout(() => {
            const topbar = document.querySelector('.swagger-ui .topbar-wrapper');
            if (topbar && !document.getElementById('session-info')) {
                const tenant = localStorage.getItem('swagger_tenant_slug') || 'not set';
                const session = localStorage.getItem('swagger_session_id') || 'not set';
                
                const info = document.createElement('div');
                info.id = 'session-info';
                info.style.cssText = `
                    color: white;
                    font-size: 0.85rem;
                    margin-left: 20px;
                    padding: 8px 12px;
                    background-color: rgba(255, 255, 255, 0.1);
                    border-radius: 6px;
                    font-family: monospace;
                `;
                
                info.innerHTML = `
                    <strong>Current Session:</strong><br>
                    Tenant: <code style="color: #86efac;">${tenant}</code><br>
                    Session: <code style="color: #93c5fd;">${session.substring(0, 20)}...</code>
                `;
                
                topbar.appendChild(info);
            }
        }, 1000);
    }

    // ========================================
    // Resaltar endpoints según el método
    // ========================================
    function highlightEndpoints() {
        const observer = new MutationObserver(function() {
            // Resaltar métodos en la lista
            document.querySelectorAll('.opblock-summary-method').forEach(method => {
                const text = method.textContent.trim();
                switch(text) {
                    case 'GET':
                        method.style.backgroundColor = '#3b82f6';
                        break;
                    case 'POST':
                        method.style.backgroundColor = '#10b981';
                        break;
                    case 'PUT':
                        method.style.backgroundColor = '#f59e0b';
                        break;
                    case 'PATCH':
                        method.style.backgroundColor = '#8b5cf6';
                        break;
                    case 'DELETE':
                        method.style.backgroundColor = '#ef4444';
                        break;
                }
            });
        });

        observer.observe(document.body, {
            childList: true,
            subtree: true
        });
    }

    // ========================================
    // Agregar tooltips útiles
    // ========================================
    function addTooltips() {
        setTimeout(() => {
            // Tooltip para botón "Authorize"
            const authorizeBtn = document.querySelector('.authorize');
            if (authorizeBtn) {
                authorizeBtn.title = 'Click here to authenticate with your JWT token';
            }

            // Tooltip para botón "Try it out"
            const tryoutButtons = document.querySelectorAll('.try-out__btn');
            tryoutButtons.forEach(btn => {
                btn.title = 'Enable this form to test the endpoint';
            });

            // Tooltip para botón "Execute"
            const executeButtons = document.querySelectorAll('.execute');
            executeButtons.forEach(btn => {
                btn.title = 'Send the request to the API';
            });
        }, 2000);
    }

    // ========================================
    // Copiar curl al portapapeles fácilmente
    // ========================================
    function enhanceCurlCopy() {
        document.addEventListener('click', function(e) {
            if (e.target.classList.contains('copy-to-clipboard')) {
                setTimeout(() => {
                    const notification = document.createElement('div');
                    notification.textContent = '? Copied to clipboard!';
                    notification.style.cssText = `
                        position: fixed;
                        top: 20px;
                        right: 20px;
                        background-color: #10b981;
                        color: white;
                        padding: 12px 20px;
                        border-radius: 8px;
                        font-weight: 600;
                        z-index: 9999;
                        box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
                        animation: slideIn 0.3s ease;
                    `;
                    
                    document.body.appendChild(notification);
                    
                    setTimeout(() => {
                        notification.style.animation = 'slideOut 0.3s ease';
                        setTimeout(() => notification.remove(), 300);
                    }, 2000);
                }, 100);
            }
        });

        // Agregar animaciones CSS
        const style = document.createElement('style');
        style.textContent = `
            @keyframes slideIn {
                from {
                    transform: translateX(400px);
                    opacity: 0;
                }
                to {
                    transform: translateX(0);
                    opacity: 1;
                }
            }
            @keyframes slideOut {
                from {
                    transform: translateX(0);
                    opacity: 1;
                }
                to {
                    transform: translateX(400px);
                    opacity: 0;
                }
            }
        `;
        document.head.appendChild(style);
    }

    // ========================================
    // Keyboard shortcuts
    // ========================================
    function setupKeyboardShortcuts() {
        document.addEventListener('keydown', function(e) {
            // Ctrl + K: Focus en el filtro de endpoints
            if (e.ctrlKey && e.key === 'k') {
                e.preventDefault();
                const filterInput = document.querySelector('.filter-container input');
                if (filterInput) {
                    filterInput.focus();
                }
            }

            // Ctrl + /: Mostrar ayuda de shortcuts
            if (e.ctrlKey && e.key === '/') {
                e.preventDefault();
                alert(`
Keyboard Shortcuts:
??????????????????
Ctrl + K: Focus en búsqueda
Ctrl + /: Mostrar esta ayuda
Esc: Cerrar modales
                `.trim());
            }
        });
    }

    // ========================================
    // Inicializar todas las funciones
    // ========================================
    function init() {
        console.log('Initializing Swagger UI enhancements...');
        
        setupHeaderAutocomplete();
        addClearSessionButton();
        showCurrentSession();
        highlightEndpoints();
        addTooltips();
        enhanceCurlCopy();
        setupKeyboardShortcuts();
        
        console.log('? Swagger UI enhancements loaded successfully');
    }

    // Esperar a que Swagger UI esté completamente cargado
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        setTimeout(init, 1000);
    }

    // ========================================
    // Exponer funciones útiles globalmente
    // ========================================
    window.SwaggerHelpers = {
        setTenant: function(slug) {
            localStorage.setItem('swagger_tenant_slug', slug);
            console.log(`? Tenant set to: ${slug}`);
            location.reload();
        },
        setSession: function(sessionId) {
            localStorage.setItem('swagger_session_id', sessionId);
            console.log(`? Session ID set to: ${sessionId}`);
            location.reload();
        },
        generateNewSession: function() {
            const newSession = 'sess_' + generateUUID();
            localStorage.setItem('swagger_session_id', newSession);
            console.log(`? New session generated: ${newSession}`);
            location.reload();
            return newSession;
        },
        clearSession: function() {
            localStorage.removeItem('swagger_tenant_slug');
            localStorage.removeItem('swagger_session_id');
            console.log('? Session cleared');
            location.reload();
        },
        getCurrentSession: function() {
            return {
                tenant: localStorage.getItem('swagger_tenant_slug'),
                sessionId: localStorage.getItem('swagger_session_id')
            };
        }
    };

    console.log(`
?????????????????????????????????????????????
?  ?? eCommerce Multi-Tenant API            ?
?  Swagger UI Enhancements Loaded           ?
?                                           ?
?  Console Helpers:                         ?
?  SwaggerHelpers.setTenant('acme')         ?
?  SwaggerHelpers.setSession('sess_123')    ?
?  SwaggerHelpers.generateNewSession()      ?
?  SwaggerHelpers.clearSession()            ?
?  SwaggerHelpers.getCurrentSession()       ?
?????????????????????????????????????????????
    `);

})();
