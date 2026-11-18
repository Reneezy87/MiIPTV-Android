package mx.miiptv.app

import android.annotation.SuppressLint
import android.app.Activity
import android.app.AlertDialog
import android.content.ActivityNotFoundException
import android.content.Intent
import android.graphics.Color
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.text.InputType
import android.view.KeyEvent
import android.view.View
import android.view.ViewGroup
import android.view.WindowManager
import android.webkit.WebChromeClient
import android.webkit.WebResourceError
import android.webkit.WebResourceRequest
import android.webkit.WebSettings
import android.webkit.WebView
import android.webkit.WebViewClient
import android.widget.EditText
import android.widget.FrameLayout
import android.widget.Toast

/**
 * Envuelve el reproductor web (index.html servido desde tu PC) en una app de Android.
 * Funciona con control remoto (Android TV / Google TV / Fire TV) y con pantalla táctil.
 */
class MainActivity : Activity() {

    private lateinit var root: FrameLayout
    private lateinit var web: WebView
    private var fullscreenView: View? = null
    private var fullscreenCallback: WebChromeClient.CustomViewCallback? = null
    private var errorShown = false

    private val prefs by lazy { getSharedPreferences("config", MODE_PRIVATE) }
    private val serverUrl: String? get() = prefs.getString("url", null)

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        root = FrameLayout(this).apply { setBackgroundColor(Color.BLACK) }
        web = WebView(this)
        root.addView(web, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT))
        setContentView(root)
        hideSystemBars()
        setupWebView()

        if (savedInstanceState != null) {
            web.restoreState(savedInstanceState)
        } else {
            val url = serverUrl
            if (url.isNullOrBlank()) askServer(firstTime = true) else web.loadUrl(url)
        }
    }

    @SuppressLint("SetJavaScriptEnabled")
    private fun setupWebView() {
        web.settings.apply {
            javaScriptEnabled = true
            domStorageEnabled = true                    // favoritos, último canal, sesión de Jellyfin
            mediaPlaybackRequiresUserGesture = false    // que el canal arranque solo
            mixedContentMode = WebSettings.MIXED_CONTENT_ALWAYS_ALLOW
            loadWithOverviewMode = true
            useWideViewPort = true
            cacheMode = WebSettings.LOAD_DEFAULT
        }
        web.setBackgroundColor(Color.BLACK)
        web.isFocusable = true
        web.isFocusableInTouchMode = true
        web.requestFocus()

        web.webViewClient = object : WebViewClient() {
            // Enlaces a otros sitios (p. ej. "Abrir sitio oficial") se abren en el navegador del equipo
            override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
                if (!request.isForMainFrame) return false
                val target = request.url
                val home = Uri.parse(serverUrl ?: return false)
                if (target.host == home.host && target.port == home.port) return false
                return try {
                    startActivity(Intent(Intent.ACTION_VIEW, target))
                    true
                } catch (e: ActivityNotFoundException) {
                    // Muchas TVs no traen navegador: se abre dentro de la app y "Atrás" regresa
                    false
                }
            }

            override fun onPageFinished(view: WebView, url: String) {
                errorShown = false
            }

            override fun onReceivedError(view: WebView, request: WebResourceRequest, error: WebResourceError) {
                if (request.isForMainFrame && !errorShown) {
                    errorShown = true
                    showConnectionError(error.description?.toString() ?: "Error de red")
                }
            }
        }

        web.webChromeClient = object : WebChromeClient() {
            // Pantalla completa del video (botón ⛶ o tecla F)
            override fun onShowCustomView(view: View, callback: CustomViewCallback) {
                if (fullscreenView != null) { callback.onCustomViewHidden(); return }
                fullscreenView = view
                fullscreenCallback = callback
                root.addView(view, FrameLayout.LayoutParams(ViewGroup.LayoutParams.MATCH_PARENT, ViewGroup.LayoutParams.MATCH_PARENT))
                web.visibility = View.GONE
                hideSystemBars()
            }

            override fun onHideCustomView() = exitFullscreen()
        }
    }

    private fun exitFullscreen() {
        val view = fullscreenView ?: return
        root.removeView(view)
        fullscreenView = null
        web.visibility = View.VISIBLE
        fullscreenCallback?.onCustomViewHidden()
        fullscreenCallback = null
        web.requestFocus()
        hideSystemBars()
    }

    // ---------- Botón Atrás ----------
    @Deprecated("Se mantiene por compatibilidad con Android 5–12")
    override fun onBackPressed() {
        if (fullscreenView != null) { exitFullscreen(); return }

        // Primero se le pregunta a la página (regresa de video → biblioteca → cuadrícula, etc.)
        web.evaluateJavascript("(window.miIptvBack && window.miIptvBack()) ? 'yes' : 'no'") { result ->
            if (result?.contains("yes") == true) return@evaluateJavascript
            if (web.canGoBack() && Uri.parse(web.url ?: "").host != Uri.parse(serverUrl ?: "").host) {
                web.goBack() // regresar de un sitio externo abierto dentro de la app
            } else {
                showExitMenu()
            }
        }
    }

    private fun showExitMenu() {
        AlertDialog.Builder(this)
            .setTitle("Mi IPTV")
            .setItems(arrayOf("Salir", "Recargar", "Cambiar servidor", "Cancelar")) { dialog, which ->
                when (which) {
                    0 -> finish()
                    1 -> web.reload()
                    2 -> askServer(firstTime = false)
                    else -> dialog.dismiss()
                }
            }
            .show()
    }

    // ---------- Control remoto ----------
    override fun dispatchKeyEvent(event: KeyEvent): Boolean {
        if (event.action == KeyEvent.ACTION_DOWN) {
            when (event.keyCode) {
                // Canal +/- del control → siguiente/anterior (RePág/AvPág en la página)
                KeyEvent.KEYCODE_CHANNEL_UP, KeyEvent.KEYCODE_MEDIA_NEXT -> { sendKeyToPage("PageDown"); return true }
                KeyEvent.KEYCODE_CHANNEL_DOWN, KeyEvent.KEYCODE_MEDIA_PREVIOUS -> { sendKeyToPage("PageUp"); return true }
                // Botón de menú o de configuración: mostrar u ocultar la lista
                KeyEvent.KEYCODE_MENU, KeyEvent.KEYCODE_GUIDE -> { sendKeyToPage("m"); return true }
                KeyEvent.KEYCODE_SETTINGS -> { showExitMenu(); return true }
                // Botón Play/Pausa del control
                KeyEvent.KEYCODE_MEDIA_PLAY_PAUSE, KeyEvent.KEYCODE_MEDIA_PLAY, KeyEvent.KEYCODE_MEDIA_PAUSE -> {
                    web.evaluateJavascript(
                        "(function(){var v=document.getElementById('video');if(v&&!v.hidden){v.paused?v.play():v.pause();}})()", null)
                    return true
                }
            }
        }
        return super.dispatchKeyEvent(event)
    }

    private fun sendKeyToPage(key: String) {
        val js = "document.dispatchEvent(new KeyboardEvent('keydown',{key:'$key',bubbles:true,cancelable:true}))"
        web.evaluateJavascript(js, null)
    }

    // ---------- Configuración del servidor ----------
    private fun askServer(firstTime: Boolean) {
        val input = EditText(this).apply {
            inputType = InputType.TYPE_CLASS_TEXT or InputType.TYPE_TEXT_VARIATION_URI
            setText(serverUrl ?: "http://192.168.1.")
            setSelection(text.length)
            setSingleLine()
        }
        val box = FrameLayout(this).apply {
            val pad = (20 * resources.displayMetrics.density).toInt()
            setPadding(pad, pad / 2, pad, 0)
            addView(input)
        }
        AlertDialog.Builder(this)
            .setTitle("Dirección del servidor")
            .setMessage("Escribe la dirección de la PC donde corre el reproductor, por ejemplo:\nhttp://192.168.1.70:8766/")
            .setView(box)
            .setCancelable(!firstTime)
            .setPositiveButton("Conectar") { _, _ ->
                val url = normalize(input.text.toString())
                if (url == null) {
                    Toast.makeText(this, "Dirección no válida", Toast.LENGTH_LONG).show()
                    askServer(firstTime)
                } else {
                    prefs.edit().putString("url", url).apply()
                    web.clearHistory()
                    web.loadUrl(url)
                }
            }
            .apply { if (!firstTime) setNegativeButton("Cancelar", null) }
            .show()
        input.requestFocus()
    }

    private fun normalize(raw: String): String? {
        var url = raw.trim()
        if (url.isEmpty()) return null
        if (!url.startsWith("http://") && !url.startsWith("https://")) url = "http://$url"
        val uri = Uri.parse(url)
        if (uri.host.isNullOrBlank()) return null
        if (!url.endsWith("/") && uri.path.isNullOrEmpty()) url += "/"
        return url
    }

    private fun showConnectionError(detail: String) {
        AlertDialog.Builder(this)
            .setTitle("No se pudo conectar")
            .setMessage("No se pudo abrir $serverUrl\n\n¿Está encendida la PC con el servidor y conectada a la misma red?\n\n($detail)")
            .setPositiveButton("Reintentar") { _, _ -> errorShown = false; web.reload() }
            .setNeutralButton("Cambiar servidor") { _, _ -> askServer(firstTime = false) }
            .setNegativeButton("Salir") { _, _ -> finish() }
            .setCancelable(false)
            .show()
    }

    // ---------- Pantalla completa del sistema ----------
    @Suppress("DEPRECATION")
    private fun hideSystemBars() {
        window.decorView.systemUiVisibility = (View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
            or View.SYSTEM_UI_FLAG_FULLSCREEN
            or View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
            or View.SYSTEM_UI_FLAG_LAYOUT_STABLE
            or View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
            or View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN)
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.P) {
            window.attributes = window.attributes.apply {
                layoutInDisplayCutoutMode = WindowManager.LayoutParams.LAYOUT_IN_DISPLAY_CUTOUT_MODE_SHORT_EDGES
            }
        }
    }

    override fun onWindowFocusChanged(hasFocus: Boolean) {
        super.onWindowFocusChanged(hasFocus)
        if (hasFocus) hideSystemBars()
    }

    // ---------- Ciclo de vida ----------
    override fun onPause() {
        web.onPause()      // pausa el video al salir de la app
        super.onPause()
    }

    override fun onResume() {
        super.onResume()
        web.onResume()
    }

    override fun onSaveInstanceState(outState: Bundle) {
        super.onSaveInstanceState(outState)
        web.saveState(outState)
    }

    override fun onDestroy() {
        web.destroy()
        super.onDestroy()
    }
}
