(function () {
    'use strict';

    if (window.__manualSubtitleExtractLoaded) return;
    window.__manualSubtitleExtractLoaded = true;

    const ACTION_ID = 'manualSubtitleExtractAction';
    const OVERLAY_ID = 'manualSubtitleExtractOverlay';
    const LOG_PREFIX = '[manual-subtitle-extract]';

    window.__manualSubtitleExtractState = {
        loadedAt: new Date().toISOString(),
        scans: 0,
        menusFound: 0,
        actionsAdded: 0,
        lastItemId: null
    };

    console.info(LOG_PREFIX, 'client loaded');

    function currentItemId() {
        const sources = [window.location.href, window.location.hash, window.location.search];
        for (const source of sources) {
            if (!source) continue;
            const match = source.match(/[?&#]id=([0-9a-fA-F-]{32,36})/i);
            if (match) return normalizeItemId(match[1]);
        }

        return null;
    }

    function normalizeItemId(value) {
        if (!value) return null;
        const match = String(value).match(/[0-9a-fA-F]{32}|[0-9a-fA-F-]{36}/);
        return match ? match[0] : null;
    }

    function itemIdFromElement(element) {
        let current = element;
        const attributes = ['data-id', 'data-itemid', 'data-item-id'];
        while (current && current !== document.documentElement) {
            for (const attribute of attributes) {
                const itemId = normalizeItemId(current.getAttribute && current.getAttribute(attribute));
                if (itemId) return itemId;
            }

            current = current.parentElement;
        }

        return null;
    }

    function basePathFromLocation() {
        const path = window.location.pathname || '';
        const match = path.match(/^(.*?)(?:\/web(?:\/|$)|\/?$)/i);
        return match && match[1] ? match[1].replace(/\/$/, '') : '';
    }

    function apiUrl(path) {
        if (window.ApiClient && typeof ApiClient.getUrl === 'function') {
            return ApiClient.getUrl(path);
        }
        return `${basePathFromLocation()}/${path.replace(/^\//, '')}`;
    }

    async function apiRequest(path, options) {
        if (window.ApiClient && typeof ApiClient.ajax === 'function') {
            return ApiClient.ajax(Object.assign({
                url: apiUrl(path),
                dataType: 'json'
            }, options || {}));
        }

        const headers = Object.assign({ 'Content-Type': 'application/json' }, (options && options.headers) || {});
        if (window.ApiClient && ApiClient.accessToken) {
            headers['X-Emby-Token'] = ApiClient.accessToken();
        }
        const response = await fetch(apiUrl(path), {
            method: (options && options.type) || 'GET',
            headers,
            body: options && options.data
        });
        if (!response.ok) throw new Error(await response.text());
        return response.status === 204 ? null : response.json();
    }

    function escapeHtml(value) {
        return String(value == null ? '' : value)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function showToast(message) {
        if (window.Dashboard && typeof Dashboard.alert === 'function') {
            Dashboard.alert(message);
        } else {
            window.alert(message);
        }
    }

    function closeOverlay() {
        const existing = document.getElementById(OVERLAY_ID);
        if (existing) existing.remove();
    }

    function injectStyles() {
        if (document.getElementById('manualSubtitleExtractStyles')) return;
        const style = document.createElement('style');
        style.id = 'manualSubtitleExtractStyles';
        style.textContent = `
            #${OVERLAY_ID}{position:fixed;inset:0;z-index:999999;background:rgba(0,0,0,.66);display:flex;align-items:center;justify-content:center;padding:1rem}
            #${OVERLAY_ID} .mse-card{width:min(680px,96vw);max-height:86vh;overflow:auto;background:#202020;color:#fff;border-radius:14px;padding:1.25rem;box-shadow:0 20px 70px rgba(0,0,0,.55)}
            #${OVERLAY_ID} h2{margin:.1rem 0 .35rem;font-size:1.4rem}
            #${OVERLAY_ID} .mse-muted{opacity:.72;margin:0 0 1rem}
            #${OVERLAY_ID} .mse-track{display:flex;gap:.75rem;align-items:flex-start;padding:.85rem;border-radius:10px;background:rgba(255,255,255,.06);margin:.55rem 0;cursor:pointer}
            #${OVERLAY_ID} .mse-track:hover{background:rgba(255,255,255,.1)}
            #${OVERLAY_ID} .mse-track.disabled{opacity:.48;cursor:not-allowed}
            #${OVERLAY_ID} .mse-track-main{font-weight:600}
            #${OVERLAY_ID} .mse-track-sub{font-size:.88rem;opacity:.68;margin-top:.15rem}
            #${OVERLAY_ID} .mse-actions{display:flex;justify-content:flex-end;gap:.65rem;margin-top:1.1rem}
            #${OVERLAY_ID} button{border:0;border-radius:9px;padding:.7rem 1rem;cursor:pointer;font-weight:600}
            #${OVERLAY_ID} .mse-primary{background:#00a4dc;color:white}
            #${OVERLAY_ID} .mse-secondary{background:rgba(255,255,255,.12);color:white}
            #${OVERLAY_ID} .mse-error{background:#6c2222;padding:.75rem;border-radius:8px;margin:.75rem 0;white-space:pre-wrap}
            #${OVERLAY_ID} .mse-spinner{display:inline-block;width:16px;height:16px;border:2px solid rgba(255,255,255,.35);border-top-color:#fff;border-radius:50%;animation:mse-spin .8s linear infinite;vertical-align:-3px;margin-right:.4rem}
            @keyframes mse-spin{to{transform:rotate(360deg)}}
        `;
        document.head.appendChild(style);
    }

    async function openDialog(itemId) {
        injectStyles();
        closeOverlay();

        const overlay = document.createElement('div');
        overlay.id = OVERLAY_ID;
        overlay.innerHTML = `
          <div class="mse-card" role="dialog" aria-modal="true">
            <h2>Extract Embedded Subtitle</h2>
            <p class="mse-muted">Choose a text subtitle track. It will be converted to SRT and saved beside the video.</p>
            <div id="mseBody"><span class="mse-spinner"></span>Reading embedded tracks…</div>
            <div class="mse-actions">
              <button class="mse-secondary" id="mseCancel">Cancel</button>
              <button class="mse-primary" id="mseExtract" disabled>Extract</button>
            </div>
          </div>`;
        document.body.appendChild(overlay);
        overlay.querySelector('#mseCancel').onclick = closeOverlay;
        overlay.addEventListener('click', e => { if (e.target === overlay) closeOverlay(); });

        try {
            const tracks = await apiRequest(`ManualSubtitleExtract/${itemId}/tracks`, { type: 'GET' });
            const body = overlay.querySelector('#mseBody');
            if (!tracks || !tracks.length) {
                body.innerHTML = '<div class="mse-error">No embedded subtitle tracks were found.</div>';
                return;
            }

            body.innerHTML = tracks.map((track, index) => {
                const textBased = track.textBased ?? track.TextBased;
                const disabled = !textBased;
                const flags = [(track.default ?? track.Default) ? 'Default' : '', (track.forced ?? track.Forced) ? 'Forced' : '', (track.hearingImpaired ?? track.HearingImpaired) ? 'SDH' : ''].filter(Boolean).join(' · ');
                return `
                  <label class="mse-track ${disabled ? 'disabled' : ''}">
                    <input type="radio" name="mseTrack" value="${track.streamIndex ?? track.StreamIndex}" ${disabled ? 'disabled' : ''}>
                    <span>
                      <div class="mse-track-main">${escapeHtml(track.language || track.Language || 'und').toUpperCase()} ${(track.title || track.Title) ? '— ' + escapeHtml(track.title || track.Title) : ''}</div>
                      <div class="mse-track-sub">Subtitle #${track.subtitleIndex ?? track.SubtitleIndex} · ${escapeHtml(track.codec || track.Codec)}${flags ? ' · ' + flags : ''}${disabled ? ' · image-based (OCR not supported)' : ''}</div>
                    </span>
                  </label>`;
            }).join('');

            const extractButton = overlay.querySelector('#mseExtract');
            body.addEventListener('change', () => {
                extractButton.disabled = !body.querySelector('input[name="mseTrack"]:checked');
            });

            extractButton.onclick = async function () {
                const checked = body.querySelector('input[name="mseTrack"]:checked');
                if (!checked) return;
                const streamIndex = Number(checked.value);
                extractButton.disabled = true;
                extractButton.innerHTML = '<span class="mse-spinner"></span>Extracting…';

                try {
                    let result;
                    try {
                        result = await apiRequest(`ManualSubtitleExtract/${itemId}/extract`, {
                            type: 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify({ StreamIndex: streamIndex, Overwrite: false })
                        });
                    } catch (firstError) {
                        const firstMessage = firstError && (firstError.responseText || firstError.message) ? (firstError.responseText || firstError.message) : String(firstError);
                        if (/already exists/i.test(firstMessage) && window.confirm('That sidecar already exists. Overwrite it? This only works if overwrite is enabled in the plugin settings.')) {
                            result = await apiRequest(`ManualSubtitleExtract/${itemId}/extract`, {
                                type: 'POST',
                                contentType: 'application/json',
                                data: JSON.stringify({ StreamIndex: streamIndex, Overwrite: true })
                            });
                        } else {
                            throw firstError;
                        }
                    }
                    closeOverlay();
                    showToast(`Subtitle extracted: ${result.fileName || result.FileName || 'done'}. Jellyfin is refreshing the item.`);
                } catch (error) {
                    const message = error && (error.responseText || error.message) ? (error.responseText || error.message) : String(error);
                    body.insertAdjacentHTML('afterbegin', `<div class="mse-error">${escapeHtml(message)}</div>`);
                    extractButton.disabled = false;
                    extractButton.textContent = 'Extract';
                }
            };
        } catch (error) {
            const body = overlay.querySelector('#mseBody');
            const message = error && (error.responseText || error.message) ? (error.responseText || error.message) : String(error);
            body.innerHTML = `<div class="mse-error">${escapeHtml(message)}</div>`;
        }
    }

    function isVisible(element) {
        return !!(element && (element.offsetParent || element.getClientRects().length));
    }

    function menuItems(menu) {
        return Array.from(menu.querySelectorAll('button, [role="menuitem"], .actionSheetMenuItem, .MuiMenuItem-root'))
            .filter(item => item.id !== ACTION_ID && isVisible(item) && !item.disabled && item.getAttribute('aria-disabled') !== 'true');
    }

    function setActionLabel(button) {
        const textNode = button.querySelector('.actionSheetItemText, .listItemBodyText, .button-text, .MuiListItemText-primary, span:last-child');
        if (textNode) {
            textNode.textContent = 'Extract Embedded Subtitle';
        } else {
            button.textContent = 'Extract Embedded Subtitle';
        }

        const icon = button.querySelector('.material-icons, .material-symbols-rounded, .material-symbols-outlined');
        if (icon) icon.textContent = 'subtitles';
    }

    function addActionToMenu(menu) {
        if (!menu || menu.querySelector('#' + ACTION_ID)) return;
        if (menu.closest && menu.closest('#' + OVERLAY_ID)) return;
        const itemId = currentItemId() || itemIdFromElement(menu);
        window.__manualSubtitleExtractState.lastItemId = itemId;
        if (!itemId) return;

        const sample = menuItems(menu)[0];
        if (!sample) return;

        const button = sample.cloneNode(true);
        button.removeAttribute('id');
        button.id = ACTION_ID;
        button.removeAttribute('data-id');
        button.removeAttribute('data-itemid');
        button.removeAttribute('data-item-id');
        button.removeAttribute('data-action');
        button.removeAttribute('disabled');
        button.removeAttribute('aria-disabled');
        button.removeAttribute('href');
        button.setAttribute('title', 'Extract Embedded Subtitle');
        if (button.tagName === 'BUTTON') {
            button.type = 'button';
        } else {
            button.setAttribute('role', 'menuitem');
            button.setAttribute('tabindex', '0');
        }

        setActionLabel(button);

        button.addEventListener('click', function (e) {
            e.preventDefault();
            e.stopPropagation();
            openDialog(itemId);
        });
        button.addEventListener('keydown', function (e) {
            if (e.key !== 'Enter' && e.key !== ' ') return;
            e.preventDefault();
            openDialog(itemId);
        });

        menu.appendChild(button);
        window.__manualSubtitleExtractState.actionsAdded += 1;
        console.info(LOG_PREFIX, 'added action menu item', itemId);
    }

    function scan() {
        window.__manualSubtitleExtractState.scans += 1;
        const menus = new Set();
        document.querySelectorAll('.actionSheet, .actionSheetContent, .actionsheet, [role="menu"], [role="dialog"] .actionSheetMenuItem, [role="dialog"] [role="menuitem"], .MuiPopover-root, .MuiMenu-paper, .MuiMenu-list, .actionSheetMenuItem, .MuiMenuItem-root').forEach(node => {
            if (!node.matches) return;
            const menu = node.matches('.actionSheetMenuItem, .MuiMenuItem-root') ? node.parentElement : node;
            if (menu && menu.querySelector) menus.add(menu);
        });

        window.__manualSubtitleExtractState.menusFound = menus.size;
        menus.forEach(menu => {
            if (menuItems(menu).length) addActionToMenu(menu);
        });
    }

    const observer = new MutationObserver(scan);
    observer.observe(document.documentElement, { childList: true, subtree: true });
    document.addEventListener('viewshow', scan, true);
    document.addEventListener('click', () => setTimeout(scan, 50), true);
    scan();
})();
