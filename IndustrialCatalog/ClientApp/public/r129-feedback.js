/* INOKSKAR Feedback Center — one notification contract for React and server-rendered pages. */
(function (win, doc) {
  'use strict';
  if (win.InokskarFeedback) return;

  var active = null;
  var timer = null;
  var lastKey = '';
  var lastAt = 0;
  var pending = [];
  var endpointRules = [
    [/^\/api\/catalog$/, 'Site içeriği kaydedildi ve yayınlandı.'],
    [/^\/api\/admin\/users(?:\/.*)?$/, 'Kullanıcı işlemi başarıyla tamamlandı.'],
    [/^\/api\/admin\/password$/, 'Şifreniz başarıyla değiştirildi.'],
    [/^\/api\/account\/password$/, 'Şifreniz başarıyla değiştirildi.'],
    [/^\/api\/admin\/mail\/settings$/, 'E-posta ayarları kaydedildi.'],
    [/^\/api\/admin\/mail\/test$/, 'Test e-postası gönderim işlemine alındı.'],
    [/^\/api\/rate\/refresh$/, 'Resmî döviz kuru güncellendi.'],
    [/^\/api\/prices\/import$/, 'Fiyatlar başarıyla içe aktarıldı.'],
    [/^\/api\/backup\/restore$/, 'Yedek geri yükleme tamamlandı.'],
    [/^\/api\/upload$/, 'Dosya başarıyla yüklendi.'],
    [/^\/api\/warranties(?:\/.*)?$/, 'Garanti işlemi başarıyla tamamlandı.'],
    [/^\/api\/technical\/device(?:\/.*)?$/, 'Teknik servis işlemi tamamlandı.'],
    [/^\/api\/customer\/profile$/, 'Profil bilgileriniz kaydedildi.'],
    [/^\/api\/inquiry$/, 'Destek talebiniz oluşturuldu.'],
    [/^\/api\/service-request$/, 'Teknik servis talebiniz oluşturuldu.'],
    [/^\/api\/inquiries(?:\/.*)?$/, 'Talep işlemi başarıyla tamamlandı.']
  ];
  // Read-only lookup, analytical events, password/login page navigation and previews
  // are intentionally excluded from automatic "saved" notifications.
  function operation(path) {
    if (/^\/api\/(?:event|inquiry-status|warranty\/query|prices\/preview)(?:\/|$)/.test(path)) return '';
    for (var i = 0; i < endpointRules.length; i++) {
      if (endpointRules[i][0].test(path)) return endpointRules[i][1];
    }
    // Future mutation endpoints automatically participate without another patch.
    return /^\/api\//.test(path) ? 'İşlem başarıyla tamamlandı.' : '';
  }
  function el(tag, css, value) {
    var node = doc.createElement(tag);
    if (css) node.className = css;
    if (value) node.textContent = value;
    return node;
  }
  function mount() {
    var root = doc.getElementById('inokskar-feedback-center');
    if (root) return root;
    if (!doc.body) return null;
    root = el('div', 'ifc-host');
    root.id = 'inokskar-feedback-center';
    root.setAttribute('aria-live', 'polite');
    root.setAttribute('aria-atomic', 'true');
    doc.body.appendChild(root);
    return root;
  }
  function dismiss() {
    if (timer) win.clearTimeout(timer);
    timer = null;
    if (active) {
      active.remove();
      active = null;
    }
  }
  function show(options) {
    var value = typeof options === 'string' ? {message: options} : (options || {});
    var tone = ['success', 'error', 'info'].includes(value.tone) ? value.tone : 'info';
    var message = String(value.message || '').replace(/\s+/g, ' ').trim().slice(0, 320);
    if (!message) return false;
    var key = tone + ':' + message;
    var now = Date.now();
    if (lastKey === key && now - lastAt < 1800) return false;
    lastKey = key; lastAt = now;
    var host = mount();
    if (!host) { pending.push({tone: tone, message: message}); return false; }
    dismiss();
    var card = el('section', 'ifc-notice ifc-' + tone);
    card.tabIndex = -1;
    card.setAttribute('role', tone === 'error' ? 'alert' : 'status');
    card.setAttribute('aria-label', tone === 'error' ? 'İşlem hatası' : tone === 'success' ? 'İşlem başarılı' : 'Bilgilendirme');
    var icon = el('span', 'ifc-icon', tone === 'error' ? '!' : tone === 'success' ? '✓' : 'i');
    icon.setAttribute('aria-hidden', 'true');
    var body = el('div', 'ifc-copy');
    body.appendChild(el('strong', '', tone === 'error' ? 'İşlem tamamlanamadı' : tone === 'success' ? 'İşlem başarılı' : 'Bilgilendirme'));
    body.appendChild(el('p', '', message));
    var close = el('button', 'ifc-close', '×');
    close.type = 'button';
    close.setAttribute('aria-label', 'Mesajı kapat');
    close.addEventListener('click', dismiss);
    card.append(icon, body, close);
    host.appendChild(card);
    active = card;
    // Fixed-center placement prevents the user having to scroll back to the
    // bottom of long forms. Keyboard and assistive-tech focus follow the result.
    try { card.focus({preventScroll:true}); } catch (_) { card.focus(); }
    if (tone !== 'error') timer = win.setTimeout(function () { if (active === card) dismiss(); }, tone === 'success' ? 8500 : 6500);
    return true;
  }
  win.InokskarFeedback = Object.freeze({
    show:show,
    success:function (message) { return show({tone:'success',message:message}); },
    error:function (message) { return show({tone:'error',message:message}); },
    info:function (message) { return show({tone:'info',message:message}); },
    dismiss:dismiss
  });
  win.addEventListener('inokskar:notify', function (e) {
    if (e && e.detail) show(e.detail);
  });
  if (doc.readyState === 'loading') {
    doc.addEventListener('DOMContentLoaded', function () {
      mount();
      while (pending.length) show(pending.shift());
    }, {once:true});
  } else {
    mount();
  }
  doc.addEventListener('keydown', function (e) {
    if (e.key === 'Escape' && active && active.contains(doc.activeElement)) dismiss();
  });

  var originalFetch = win.fetch;
  if (typeof originalFetch !== 'function') return;
  win.fetch = async function () {
    var args = arguments;
    var input = args[0], init = args[1] || {};
    var method = String(init.method || (input && input.method) || 'GET').toUpperCase();
    var url;
    try { url = new URL(typeof input === 'string' ? input : input.url, win.location.href); }
    catch (_) { return originalFetch.apply(this, args); }
    var enabled = url.origin === win.location.origin &&
      /^(POST|PUT|PATCH|DELETE)$/.test(method) &&
      win.location.pathname !== '/admin' &&
      !!operation(url.pathname);
    if (!enabled) return originalFetch.apply(this, args);
    var summary = operation(url.pathname);
    try {
      var response = await originalFetch.apply(this, args);
      var body = null;
      var contentType = response.headers.get('content-type') || '';
      if (contentType.includes('application/json')) {
        try { body = await response.clone().json(); } catch (_) { /* non-JSON response */ }
      }
      if (!response.ok || (body && (body.ok === false || body.error))) {
        show({tone:'error',message:body && typeof body.error === 'string' ? body.error : response.status === 401 ? 'Oturumunuz sona erdi. Lütfen yeniden giriş yapın.' : response.status === 403 ? 'Bu işlem için yetkiniz bulunmuyor.' : 'İşlem gerçekleştirilemedi. Lütfen yeniden deneyin.'});
      } else {
        show({tone:'success',message:summary});
      }
      return response;
    } catch (e) {
      show({tone:'error',message:'Bağlantı kurulamadı. İnternet bağlantınızı kontrol ederek yeniden deneyin.'});
      throw e;
    }
  };
})(window, document);
