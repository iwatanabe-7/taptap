// スマホのブラウザで開かれたとき、ページを端末幅に合わせてキャンバスを画面いっぱい (縦) に広げる。
// unityroom の再生ページには viewport 指定がなく、PC 幅 (980px) で縮小表示されてしまうため。
mergeInto(LibraryManager.library, {
  TapTap_SetupMobileLayout: function () {
    var ua = navigator.userAgent;
    var isMobile = /Android|iPhone|iPad|iPod|Mobile/i.test(ua) ||
      (navigator.maxTouchPoints > 1 && /Macintosh/.test(ua)); // iPadOS は Mac を名乗る
    if (!isMobile) return 0;

    var meta = document.querySelector('meta[name="viewport"]');
    if (!meta) {
      meta = document.createElement('meta');
      meta.name = 'viewport';
      document.head.appendChild(meta);
    }
    meta.content = 'width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no, viewport-fit=cover';

    var canvas = (typeof Module !== 'undefined' && Module.canvas) || document.querySelector('canvas');
    if (canvas) canvas.classList.add('taptap-fullscreen');

    // ページ側のスクリプトがキャンバスの inline style を書き換えても負けないよう、!important のスタイルシートで指定する
    var style = document.createElement('style');
    style.textContent =
      'html,body{margin:0!important;padding:0!important;width:100%!important;height:100%!important;' +
      'overflow:hidden!important;overscroll-behavior:none!important;background:#05060f!important;}' +
      'canvas.taptap-fullscreen{position:fixed!important;left:0!important;top:0!important;' +
      'width:100vw!important;height:100vh!important;height:100dvh!important;' +
      'margin:0!important;z-index:2147483647!important;touch-action:none!important;}';
    document.head.appendChild(style);
    window.scrollTo(0, 0);
    return 1;
  }
});
