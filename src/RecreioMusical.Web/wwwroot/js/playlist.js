(function () {
    'use strict';

    const NICKNAME_KEY = 'recreioMusical.nickname';
    const PIN_KEY = 'recreioMusical.adminPin';
    const SEARCH_DELAY_MS = 380;
    const DEFAULT_EMPTY_TITLE = 'Nenhuma busca por aqui ainda';
    const DEFAULT_EMPTY_TEXT = 'As faixas encontradas no Spotify vão aparecer neste espaço, com capa, artistas e duração.';

    const icons = {
        play: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M9 7.5v9l8-4.5-8-4.5Z" fill="currentColor"/></svg>',
        external: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M14 4h6v6M20 4l-8 8" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"/><path d="M18 14v4a2 2 0 0 1-2 2H6a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2h4" stroke="currentColor" stroke-width="1.9" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        remove: '<svg width="14" height="14" viewBox="0 0 24 24" fill="none" aria-hidden="true"><path d="M5 7h14M9 7V5h6v2M8 10v8M12 10v8M16 10v8M6 7l1 13h10l1-13" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>'
    };

    const dom = {
        searchForm: document.getElementById('search-form'),
        searchInput: document.getElementById('search-input'),
        searchButton: document.getElementById('search-button'),
        searchResults: document.getElementById('search-results'),
        searchEmpty: document.getElementById('search-empty'),
        searchEmptyTitle: document.querySelector('#search-empty .rm-empty-title'),
        searchEmptyText: document.querySelector('#search-empty .rm-empty-text'),
        searchFeedback: document.getElementById('search-feedback'),
        nickname: document.getElementById('nickname'),
        nicknameHint: document.getElementById('nickname-hint'),
        playlistCount: document.getElementById('playlist-count'),
        playlistMeter: document.getElementById('playlist-meter'),
        playlistStatus: document.getElementById('playlist-status'),
        slots: document.getElementById('playlist-slots'),
        player: document.getElementById('player'),
        playerFrame: document.getElementById('spotify-player'),
        playerTitle: document.getElementById('player-title'),
        playerClose: document.getElementById('player-close'),
        clearButton: document.getElementById('clear-playlist'),
        liveStatus: document.getElementById('live-status'),
        liveLabel: document.getElementById('live-label'),
        toast: document.getElementById('toast'),
        pinDialog: document.getElementById('pin-dialog'),
        pinForm: document.getElementById('pin-form'),
        pinMessage: document.getElementById('pin-dialog-message'),
        pinInput: document.getElementById('pin-input'),
        pinError: document.getElementById('pin-error'),
        pinCancel: document.getElementById('pin-cancel'),
        pinConfirm: document.getElementById('pin-confirm')
    };

    const defaultNicknameHint = dom.nicknameHint.textContent.trim();

    const state = {
        playlist: readInitialPlaylist(),
        adminEnabled: dom.slots.dataset.adminEnabled === 'true',
        searchEnabled: !dom.searchInput.disabled,
        searchTimer: 0,
        searchController: null,
        addingTrackId: null,
        pendingAdminAction: null,
        adminPin: readStoredValue(PIN_KEY),
        toastTimer: 0,
        lastRefresh: 0
    };

    // ---------------------------------------------------------------------
    // Playlist
    // ---------------------------------------------------------------------

    function normalizePlaylist(data) {
        const items = Array.isArray(data && data.items)
            ? data.items
                .filter(function (item) {
                    return item && Number.isInteger(Number(item.slot));
                })
                .map(function (item) {
                    return Object.assign({}, item, { slot: Number(item.slot) });
                })
                .sort(function (a, b) {
                    return a.slot - b.slot;
                })
            : [];

        const capacity = Math.max(1, Number(data && data.capacity) || 5);

        return {
            items: items,
            capacity: capacity,
            count: items.length,
            isFull: items.length >= capacity
        };
    }

    function readInitialPlaylist() {
        const node = document.getElementById('initial-playlist');
        if (node) {
            try {
                return normalizePlaylist(JSON.parse(node.textContent || '{}'));
            } catch (error) {
                // O fetch inicial abaixo recupera o estado.
            }
        }

        return normalizePlaylist(null);
    }

    function applyPlaylist(data) {
        state.playlist = normalizePlaylist(data);
        renderPlaylist();
    }

    function renderPlaylist() {
        const playlist = state.playlist;
        const itemsBySlot = new Map();
        playlist.items.forEach(function (item) {
            itemsBySlot.set(item.slot, item);
        });

        const fragment = document.createDocumentFragment();
        for (let slot = 1; slot <= playlist.capacity; slot += 1) {
            fragment.appendChild(itemsBySlot.has(slot)
                ? createFilledSlot(slot, itemsBySlot.get(slot))
                : createEmptySlot(slot));
        }

        dom.slots.replaceChildren(fragment);
        dom.playlistCount.textContent = playlist.count + '/' + playlist.capacity;

        const meter = document.createDocumentFragment();
        for (let slot = 1; slot <= playlist.capacity; slot += 1) {
            const segment = el('span', 'rm-meter-segment');
            if (itemsBySlot.has(slot)) {
                segment.classList.add('rm-meter-segment--filled');
            }
            meter.appendChild(segment);
        }

        dom.playlistMeter.replaceChildren(meter);
        dom.playlistMeter.setAttribute(
            'aria-label',
            playlist.count + ' de ' + playlist.capacity + ' vagas ocupadas');

        if (playlist.count === 0) {
            dom.playlistStatus.textContent = 'Nenhuma música escolhida ainda.';
        } else if (playlist.isFull) {
            dom.playlistStatus.textContent = 'Playlist completa: só a administração libera espaço.';
        } else {
            dom.playlistStatus.textContent = playlist.count + ' de ' + playlist.capacity + ' vagas ocupadas.';
        }

        updateSearchActions();
    }

    function createFilledSlot(slot, item) {
        const li = el('li', 'rm-slot rm-slot--filled');
        li.dataset.slot = String(slot);

        const index = el('span', 'rm-slot-index', String(slot).padStart(2, '0'));
        index.setAttribute('aria-hidden', 'true');

        const cover = el('span', 'rm-slot-cover');
        cover.setAttribute('aria-hidden', 'true');
        appendCover(cover, item.coverUrl);

        const body = el('span', 'rm-slot-body');
        const title = el('span', 'rm-slot-title', item.title || 'Música sem título');
        title.title = item.title || '';
        const subtitle = el(
            'span',
            'rm-slot-subtitle',
            (item.artists || 'Artista desconhecido') + ' · por ' + (item.addedBy || 'alguém'));
        body.append(title, subtitle);

        const actions = el('span', 'rm-slot-actions');

        const play = el('button', 'rm-icon-button rm-icon-button--play');
        play.type = 'button';
        play.dataset.playSlot = String(slot);
        play.setAttribute('aria-label', 'Tocar ' + (item.title || 'música'));
        play.innerHTML = icons.play;
        actions.appendChild(play);

        const link = el('a', 'rm-icon-button');
        link.target = '_blank';
        link.rel = 'noopener noreferrer';
        link.setAttribute('aria-label', 'Abrir ' + (item.title || 'música') + ' no Spotify');
        link.innerHTML = icons.external;
        if (isHttpUrl(item.spotifyUrl)) {
            link.href = item.spotifyUrl;
        }
        actions.appendChild(link);

        if (state.adminEnabled) {
            const remove = el('button', 'rm-icon-button rm-icon-button--danger');
            remove.type = 'button';
            remove.dataset.removeSlot = String(slot);
            remove.setAttribute('aria-label', 'Remover ' + (item.title || 'música'));
            remove.innerHTML = icons.remove;
            actions.appendChild(remove);
        }

        li.append(index, cover, body, actions);
        return li;
    }

    function createEmptySlot(slot) {
        const li = el('li', 'rm-slot rm-slot--empty');
        li.dataset.slot = String(slot);

        const index = el('span', 'rm-slot-index', String(slot).padStart(2, '0'));
        index.setAttribute('aria-hidden', 'true');

        const body = el('span', 'rm-slot-body');
        body.append(
            el('span', 'rm-slot-title', 'Vaga disponível'),
            el('span', 'rm-slot-subtitle', 'Aguardando alguém escolher'));

        const dot = el('span', 'rm-slot-dot');
        dot.setAttribute('aria-hidden', 'true');

        li.append(index, body, dot);
        return li;
    }

    function appendCover(container, coverUrl) {
        if (isHttpUrl(coverUrl)) {
            const img = document.createElement('img');
            img.src = coverUrl;
            img.alt = '';
            img.loading = 'lazy';
            img.decoding = 'async';
            img.referrerPolicy = 'no-referrer';
            img.addEventListener('error', function () {
                img.remove();
                container.appendChild(el('span', 'rm-slot-cover-fallback', '♪'));
            });
            container.appendChild(img);
        } else {
            container.appendChild(el('span', 'rm-slot-cover-fallback', '♪'));
        }
    }

    function findItem(slot) {
        return state.playlist.items.find(function (item) {
            return item.slot === slot;
        }) || null;
    }

    function playSlot(slot) {
        const item = findItem(slot);
        if (!item || !/^[A-Za-z0-9]{22}$/.test(String(item.trackId || ''))) {
            showToast('Não foi possível abrir o player desta música.', 'error');
            return;
        }

        dom.playerTitle.textContent = item.title + ' — ' + item.artists;
        dom.playerFrame.src = 'https://open.spotify.com/embed/track/'
            + encodeURIComponent(item.trackId)
            + '?utm_source=generator&theme=0';
        dom.player.hidden = false;
        dom.player.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function closePlayer() {
        dom.player.hidden = true;
        dom.playerFrame.removeAttribute('src');
        dom.playerTitle.textContent = 'Escolha uma música';
    }

    // ---------------------------------------------------------------------
    // Busca
    // ---------------------------------------------------------------------

    function onSearchInput() {
        if (!state.searchEnabled) {
            return;
        }

        clearTimeout(state.searchTimer);

        const query = dom.searchInput.value.trim();

        if (query.length < 2) {
            if (state.searchController) {
                state.searchController.abort();
                state.searchController = null;
            }
            setSearchBusy(false);
            hideFeedback();
            dom.searchResults.hidden = true;
            dom.searchResults.replaceChildren();
            showSearchEmpty(DEFAULT_EMPTY_TITLE, DEFAULT_EMPTY_TEXT);
            return;
        }

        state.searchTimer = window.setTimeout(function () {
            runSearch(query);
        }, SEARCH_DELAY_MS);
    }

    function onSearchSubmit(event) {
        event.preventDefault();

        if (!state.searchEnabled) {
            return;
        }

        clearTimeout(state.searchTimer);

        const query = dom.searchInput.value.trim();
        if (query.length < 2) {
            showFeedback('Digite pelo menos 2 caracteres para buscar.', 'error');
            dom.searchInput.focus();
            return;
        }

        runSearch(query);
    }

    async function runSearch(query) {
        if (state.searchController) {
            state.searchController.abort();
        }

        const controller = new AbortController();
        state.searchController = controller;
        setSearchBusy(true);
        showFeedback('Buscando no Spotify…', 'info');
        showSearchEmpty('Buscando no Spotify…', 'Aguarde um instante.');
        dom.searchResults.hidden = true;

        try {
            const data = await requestJson(
                '/api/tracks/search?q=' + encodeURIComponent(query),
                { signal: controller.signal });

            if (controller.signal.aborted) {
                return;
            }

            hideFeedback();
            renderSearchResults(Array.isArray(data && data.tracks) ? data.tracks : []);
        } catch (error) {
            if (error.name === 'AbortError') {
                return;
            }

            showFeedback(error.message, 'error');
            dom.searchResults.hidden = true;
            dom.searchResults.replaceChildren();
            showSearchEmpty('Não foi possível buscar agora.', error.message);
        } finally {
            if (state.searchController === controller) {
                state.searchController = null;
                setSearchBusy(false);
            }
        }
    }

    function renderSearchResults(tracks) {
        if (tracks.length === 0) {
            dom.searchResults.hidden = true;
            dom.searchResults.replaceChildren();
            showSearchEmpty(
                'Nenhuma música encontrada',
                'Tente outro nome de música, artista ou álbum.');
            return;
        }

        const fragment = document.createDocumentFragment();
        tracks.forEach(function (track) {
            fragment.appendChild(createSearchResult(track));
        });

        dom.searchResults.replaceChildren(fragment);
        dom.searchResults.hidden = false;
        hideSearchEmpty();
        updateSearchActions();
    }

    function createSearchResult(track) {
        const li = el('li', 'rm-result');

        const cover = el('span', 'rm-result-cover');
        cover.setAttribute('aria-hidden', 'true');
        appendCover(cover, track.coverUrl);

        const body = el('span', 'rm-result-body');
        const title = el('span', 'rm-result-title', track.title || 'Música sem título');
        title.title = track.title || '';
        const artists = el('span', 'rm-result-subtitle', track.artists || 'Artista desconhecido');
        const metaParts = [];
        if (track.albumName) {
            metaParts.push(track.albumName);
        }
        metaParts.push(formatDuration(track.durationMs));
        const meta = el('span', 'rm-result-meta', metaParts.join(' · '));
        body.append(title, artists, meta);

        const action = el('button', 'rm-result-action');
        action.type = 'button';
        action.dataset.addTrackId = String(track.id || '');
        action.textContent = 'Adicionar';

        li.append(cover, body, action);
        return li;
    }

    function updateSearchActions() {
        if (!dom.searchResults) {
            return;
        }

        const addedIds = new Set(state.playlist.items.map(function (item) {
            return item.trackId;
        }));

        dom.searchResults.querySelectorAll('[data-add-track-id]').forEach(function (button) {
            const trackId = button.getAttribute('data-add-track-id');
            const added = addedIds.has(trackId);
            const busy = state.addingTrackId === trackId;

            button.disabled = added || state.playlist.isFull || busy;
            button.textContent = added
                ? 'Já está'
                : state.playlist.isFull
                    ? 'Playlist cheia'
                    : busy
                        ? 'Adicionando…'
                        : 'Adicionar';
        });
    }

    async function addTrack(trackId) {
        if (!trackId || state.addingTrackId) {
            return;
        }

        if (state.playlist.isFull) {
            showToast('A playlist já está completa com 5 músicas.', 'error');
            return;
        }

        const nickname = dom.nickname.value.trim();

        if (nickname.length < 2) {
            setNicknameHint('Informe um apelido com pelo menos 2 caracteres.', true);
            dom.nickname.focus();
            return;
        }

        saveNickname(nickname);
        state.addingTrackId = trackId;
        updateSearchActions();

        try {
            const data = await requestJson('/api/playlist', {
                method: 'POST',
                body: JSON.stringify({ trackId: trackId, addedBy: nickname })
            });

            if (data && data.playlist) {
                applyPlaylist(data.playlist);
            }

            showToast('Música adicionada à playlist!');
        } catch (error) {
            showToast(error.message, 'error');
        } finally {
            state.addingTrackId = null;
            updateSearchActions();
        }
    }

    // ---------------------------------------------------------------------
    // Administração e PIN
    // ---------------------------------------------------------------------

    function requestRemove(slot) {
        const item = findItem(slot);
        if (!item) {
            return;
        }

        requestAdmin({ type: 'remove', slot: slot, label: item.title });
    }

    async function requestAdmin(action) {
        if (!state.adminEnabled) {
            return;
        }

        if (state.adminPin) {
            try {
                await executeAdmin(action, state.adminPin);
                return;
            } catch (error) {
                if (error.status === 401) {
                    clearStoredPin();
                } else {
                    showToast(error.message, 'error');
                    return;
                }
            }
        }

        openPinDialog(action);
    }

    async function executeAdmin(action, pin) {
        const path = action.type === 'remove'
            ? '/api/playlist/' + action.slot
            : '/api/playlist';

        await requestJson(path, {
            method: 'DELETE',
            headers: { 'X-Admin-Pin': pin }
        });

        storePin(pin);
        await refreshPlaylist();
        closePinDialog();
        showToast(action.type === 'remove' ? 'Música removida da playlist.' : 'Playlist limpa.');
    }

    function openPinDialog(action) {
        state.pendingAdminAction = action;
        dom.pinMessage.textContent = action.type === 'remove'
            ? 'Remover “' + action.label + '” da playlist?'
            : 'Limpar todas as músicas da playlist? Essa ação não pode ser desfeita.';
        dom.pinError.hidden = true;
        dom.pinInput.value = '';
        dom.pinDialog.showModal();
        window.setTimeout(function () {
            dom.pinInput.focus();
        }, 0);
    }

    function closePinDialog() {
        state.pendingAdminAction = null;
        dom.pinInput.value = '';
        dom.pinError.hidden = true;

        if (dom.pinDialog.open) {
            dom.pinDialog.close();
        }
    }

    async function onPinSubmit(event) {
        event.preventDefault();

        if (!state.pendingAdminAction) {
            closePinDialog();
            return;
        }

        const pin = dom.pinInput.value;
        if (!pin) {
            showPinError('Digite o PIN.');
            return;
        }

        setPinBusy(true);

        try {
            await executeAdmin(state.pendingAdminAction, pin);
        } catch (error) {
            if (error.status === 401) {
                showPinError('PIN incorreto. Tente novamente.');
                dom.pinInput.select();
            } else if (error.status === 429) {
                showPinError('Muitas tentativas. Aguarde ' + retrySeconds(error) + ' segundos.');
            } else {
                showPinError(error.message);
            }
        } finally {
            setPinBusy(false);
        }
    }

    function showPinError(message) {
        dom.pinError.textContent = message;
        dom.pinError.hidden = false;
    }

    function setPinBusy(busy) {
        dom.pinConfirm.disabled = busy;
        dom.pinConfirm.textContent = busy ? 'Verificando…' : 'Confirmar';
    }

    function retrySeconds(error) {
        const seconds = Number(error.retryAfter);
        return Number.isFinite(seconds) && seconds > 0 ? Math.ceil(seconds) : 30;
    }

    // ---------------------------------------------------------------------
    // Ao vivo (SSE) e atualização manual
    // ---------------------------------------------------------------------

    function connectEvents() {
        if (!('EventSource' in window)) {
            setLiveStatus('offline');
            return;
        }

        let source;
        try {
            source = new EventSource('/api/playlist/events');
        } catch (error) {
            setLiveStatus('offline');
            return;
        }

        source.addEventListener('open', function () {
            setLiveStatus('live');
        });

        source.addEventListener('playlist', function (event) {
            if (!event.data) {
                return;
            }

            try {
                applyPlaylist(JSON.parse(event.data));
                setLiveStatus('live');
            } catch (error) {
                // Evento malformado: o próximo snapshot corrige.
            }
        });

        source.addEventListener('error', function () {
            setLiveStatus(source.readyState === EventSource.CLOSED ? 'offline' : 'reconnecting');
        });

        window.addEventListener('pagehide', function () {
            source.close();
        }, { once: true });
    }

    function setLiveStatus(status) {
        const labels = {
            connecting: 'Conectando à playlist ao vivo…',
            live: 'Playlist ao vivo para todos',
            reconnecting: 'Reconectando à playlist…',
            offline: 'Sem atualização automática'
        };

        dom.liveStatus.dataset.state = status;
        dom.liveLabel.textContent = labels[status] || labels.offline;
    }

    async function refreshPlaylist() {
        try {
            const data = await requestJson('/api/playlist');
            applyPlaylist(data);
            state.lastRefresh = Date.now();
        } catch (error) {
            // O SSE continua tentando; não vale interromper a pessoa.
        }
    }

    // ---------------------------------------------------------------------
    // Utilidades
    // ---------------------------------------------------------------------

    async function requestJson(url, options) {
        const settings = options || {};
        const headers = new Headers(settings.headers || {});
        headers.set('Accept', 'application/json');

        if (settings.body) {
            headers.set('Content-Type', 'application/json');
        }

        let response;
        try {
            response = await fetch(url, Object.assign({}, settings, { headers: headers }));
        } catch (error) {
            if (error.name === 'AbortError') {
                throw error;
            }

            const networkError = new Error('Sem conexão com o servidor. Tente novamente.');
            networkError.cause = error;
            throw networkError;
        }

        if (response.status === 204) {
            return null;
        }

        let payload = null;
        const contentType = response.headers.get('content-type') || '';
        if (contentType.includes('json')) {
            try {
                payload = await response.json();
            } catch (error) {
                payload = null;
            }
        }

        if (!response.ok) {
            const error = new Error(
                payload && payload.detail
                    ? payload.detail
                    : 'Não foi possível concluir (' + response.status + ').');
            error.status = response.status;
            error.code = payload && payload.code;
            error.retryAfter = response.headers.get('retry-after');
            throw error;
        }

        return payload;
    }

    function el(tagName, className, text) {
        const node = document.createElement(tagName);
        if (className) {
            node.className = className;
        }
        if (typeof text === 'string') {
            node.textContent = text;
        }
        return node;
    }

    function formatDuration(durationMs) {
        const totalSeconds = Math.max(0, Math.round((Number(durationMs) || 0) / 1000));
        const minutes = Math.floor(totalSeconds / 60);
        const seconds = totalSeconds % 60;
        return minutes + ':' + String(seconds).padStart(2, '0');
    }

    function isHttpUrl(value) {
        if (typeof value !== 'string' || value.length === 0) {
            return false;
        }

        try {
            const url = new URL(value);
            return url.protocol === 'https:' || url.protocol === 'http:';
        } catch (error) {
            return false;
        }
    }

    function showSearchEmpty(title, text) {
        dom.searchEmptyTitle.textContent = title;
        dom.searchEmptyText.textContent = text;
        dom.searchEmpty.hidden = false;
    }

    function hideSearchEmpty() {
        dom.searchEmpty.hidden = true;
    }

    function showFeedback(message, tone) {
        dom.searchFeedback.textContent = message;
        dom.searchFeedback.className = 'rm-feedback rm-feedback--' + tone;
        dom.searchFeedback.hidden = false;
    }

    function hideFeedback() {
        dom.searchFeedback.textContent = '';
        dom.searchFeedback.hidden = true;
    }

    function setSearchBusy(busy) {
        dom.searchButton.disabled = busy || !state.searchEnabled;
        dom.searchButton.textContent = busy ? 'Buscando…' : 'Buscar';
        if (busy) {
            dom.searchInput.setAttribute('aria-busy', 'true');
        } else {
            dom.searchInput.removeAttribute('aria-busy');
        }
    }

    function setNicknameHint(message, isError) {
        dom.nicknameHint.textContent = message;
        dom.nicknameHint.classList.toggle('rm-identity-hint--error', Boolean(isError));
    }

    function saveNickname(value) {
        const trimmed = value.trim();
        try {
            if (trimmed.length === 0) {
                localStorage.removeItem(NICKNAME_KEY);
            } else {
                localStorage.setItem(NICKNAME_KEY, trimmed);
            }
        } catch (error) {
            // Armazenamento indisponível (modo privado); o app segue funcionando.
        }
    }

    function readStoredValue(key) {
        try {
            return sessionStorage.getItem(key) || '';
        } catch (error) {
            return '';
        }
    }

    function storePin(pin) {
        state.adminPin = pin;
        try {
            sessionStorage.setItem(PIN_KEY, pin);
        } catch (error) {
            // Sem sessionStorage, o PIN vale só para esta página.
        }
    }

    function clearStoredPin() {
        state.adminPin = '';
        try {
            sessionStorage.removeItem(PIN_KEY);
        } catch (error) {
            // Ignora ambientes sem sessionStorage.
        }
    }

    function showToast(message, tone) {
        dom.toast.textContent = message;
        dom.toast.className = 'rm-toast rm-toast--' + (tone || 'success');
        dom.toast.hidden = false;
        clearTimeout(state.toastTimer);
        state.toastTimer = window.setTimeout(function () {
            dom.toast.hidden = true;
        }, 4500);
    }

    // ---------------------------------------------------------------------
    // Inicialização
    // ---------------------------------------------------------------------

    function init() {
        if (state.searchEnabled) {
            dom.searchForm.addEventListener('submit', onSearchSubmit);
            dom.searchInput.addEventListener('input', onSearchInput);
        }

        dom.nickname.addEventListener('input', function () {
            saveNickname(dom.nickname.value);
            setNicknameHint(defaultNicknameHint, false);
        });

        dom.searchResults.addEventListener('click', function (event) {
            const target = event.target instanceof Element
                ? event.target.closest('[data-add-track-id]')
                : null;

            if (target) {
                addTrack(target.getAttribute('data-add-track-id'));
            }
        });

        dom.slots.addEventListener('click', function (event) {
            const target = event.target instanceof Element ? event.target : null;
            if (!target) {
                return;
            }

            const playButton = target.closest('[data-play-slot]');
            if (playButton) {
                playSlot(Number(playButton.dataset.playSlot));
                return;
            }

            const removeButton = target.closest('[data-remove-slot]');
            if (removeButton) {
                requestRemove(Number(removeButton.dataset.removeSlot));
            }
        });

        dom.playerClose.addEventListener('click', closePlayer);

        if (dom.clearButton) {
            dom.clearButton.addEventListener('click', function () {
                requestAdmin({ type: 'clear' });
            });
        }

        dom.pinForm.addEventListener('submit', onPinSubmit);
        dom.pinCancel.addEventListener('click', closePinDialog);
        dom.pinDialog.addEventListener('cancel', function () {
            state.pendingAdminAction = null;
        });

        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && Date.now() - state.lastRefresh > 5000) {
                refreshPlaylist();
            }
        });

        applyPlaylist(state.playlist);

        const storedNickname = readStoredValue(NICKNAME_KEY);
        if (storedNickname) {
            dom.nickname.value = storedNickname;
        }

        connectEvents();

        if (!state.playlist.items.length) {
            // Garante que a tela reflita o banco mesmo se o HTML estava em cache.
            refreshPlaylist();
        }
    }

    init();
})();
