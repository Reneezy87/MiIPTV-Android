plugins {
    id("com.android.application")
    id("org.jetbrains.kotlin.android")
}

android {
    namespace = "mx.miiptv.app"
    compileSdk = 35

    defaultConfig {
        applicationId = "mx.miiptv.app"
        minSdk = 21          // Android 5.0: cubre casi cualquier TV o celular
        targetSdk = 35
        versionCode = 1
        versionName = "1.0"
    }

    buildTypes {
        release {
            isMinifyEnabled = false
            // Para uso personal se firma con la llave de depuración; así "Build APK" da un APK instalable
            signingConfig = signingConfigs.getByName("debug")
        }
    }

    compileOptions {
        sourceCompatibility = JavaVersion.VERSION_17
        targetCompatibility = JavaVersion.VERSION_17
    }
    kotlinOptions {
        jvmTarget = "17"
    }
}

// Sin dependencias externas: solo usa el WebView del sistema
