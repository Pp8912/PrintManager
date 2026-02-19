# Guía de Configuración e Instalación
## PrintManager - Impresora Virtual (Negro a Cian)

Esta guía explica cómo configurar el sistema para que intercepte sus impresiones y las redirija a nuestra aplicación.

### 1. Requisitos Previos
*   **Ghostscript**: Debe instalar Ghostscript en su sistema.
    *   Descargar: [Ghostscript Downloads](https://ghostscript.com/releases/gsdnld.html)
    *   Instalar la versión "Ghostscript AGPL Release" (ej. 10.x).
*   **clawPDF**: Necesitamos este driver de impresora virtual.
    *   Descargar e instalar: [clawPDF en GitHub](https://github.com/clawsoftware/clawPDF)

### 2. Configuración de clawPDF
1.  Abra **clawPDF Settings**.
2.  Cree un nuevo perfil llamado `Impresora Color Virtual`.
3.  En la configuración del perfil:
    *   **Formato**: PDF
    *   **Directorio de destino**: `C:\Windows\Temp` (o cualquier carpeta temporal).
    *   **Acciones posteriores (Post processing)**:
    *   Active "Run Application" (Ejecutar aplicación).
    *   **Application Path**: Busque y seleccione el ejecutable que hemos compilado:
        *   Ruta: `f:\!!!AAAA\Proyectos\ImpresoraVirtual\PrintManager\bin\Debug\net8.0-windows\PrintManager.exe`
    *   **Arguments**: Escriba literalmente: `"${file}"`
        *   (Incluya las comillas y el símbolo de dólar).
4.  Guarde el perfil.
5.  Vaya a la pestaña "Printers" (Impresoras) en clawPDF.
6.  Instale una nueva impresora llamada `CyanPrinter` asignada a este perfil.

### 3. Uso
1.  Abra cualquier documento (Word, Excel, Chrome).
2.  Imprima (`Ctrl+P`).
3.  Seleccione la impresora `CyanPrinter`.
4.  Automáticamente se abrirá nuestra ventana **PrintManager**.
5.  Verifique que la opción "Modo Tinta: SOLO CYAN" esté marcada.
6.  Haga clic en **IMPRIMIR**.
7.  El documento saldrá por su impresora física convertido a azul.
