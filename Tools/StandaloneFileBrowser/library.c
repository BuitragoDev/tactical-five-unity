#include <stddef.h>
#include <gtk/gtk.h>
#include <gdk/gdkx.h>
#include <X11/Xlib.h>
#include <string.h>
#include <malloc.h>
#include "library.h"

char* strcat_realloc(char* str1, const char* str2) {
    char* new_str;
    size_t length;
    size_t str1length;

    if (str2 == NULL)
        return str1;
    str1length = 0;
    if (str1 != NULL)
        str1length = strlen(str1);
    length = strlen(str2) + str1length;
    new_str = (char*) realloc(str1, (1 + length) * sizeof(char));
    if (new_str == NULL)
        return str1;
    new_str[str1length] = '\0';

    strcat(new_str, str2);

    return new_str;
}

static callbackFunc asyncCallback;

void DialogInit() {
    gtk_init(0, NULL);
}

const char*
GTKOpenPanel(const char* title, const char* directory, const char* extension, bool multiselect,
             GtkFileChooserAction action);

const char*
GTKSavePanel(const char* title, const char* directory, const char* defaultName, const char* filters);

void GTKSetFilters(const char* extension, GtkWidget* dialog);

// ─────────────────────────────────────────────────────────────────────────
// PATCH: asociar el diálogo a la ventana activa para que aparezca delante.
// El plugin original crea el diálogo con padre NULL, así que en fullscreen el
// gestor de ventanas (GNOME) lo deja detrás y muestra la notificación "window
// is ready". Aquí obtenemos la ventana X11 activa (la de Unity) y la fijamos
// como padre transitorio, además de pedir keep-above + present.
// ─────────────────────────────────────────────────────────────────────────

// Devuelve el XID de la ventana top-level activa.
static Window GetActiveTopLevelWindow() {
    GdkDisplay* gdk_display = gdk_display_get_default();
    if (!gdk_display) return 0;
    Display* dpy = GDK_DISPLAY_XDISPLAY(gdk_display);
    if (!dpy) return 0;

    Window root = DefaultRootWindow(dpy);
    Window active = 0;

    // Preferir el EWMH _NET_ACTIVE_WINDOW del root.
    Atom prop = XInternAtom(dpy, "_NET_ACTIVE_WINDOW", False);
    Atom type;
    int format;
    unsigned long nitems, bytes_after;
    unsigned char* data = NULL;
    if (XGetWindowProperty(dpy, root, prop, 0, 1, False, XA_WINDOW,
                           &type, &format, &nitems, &bytes_after, &data) == Success && data != NULL) {
        active = *(Window*)data;
        XFree(data);
    }

    // Fallback: foco de entrada.
    if (active == 0) {
        Window focus = 0;
        int revert = 0;
        XGetInputFocus(dpy, &focus, &revert);
        active = focus;
    }
    if (active == 0) return 0;

    // Subir hasta la ventana top-level.
    Window w = active;
    while (w != 0) {
        Window wroot = 0, parent = 0, *children = NULL;
        unsigned int n = 0;
        if (XQueryTree(dpy, w, &wroot, &parent, &children, &n) == 0) break;
        if (children) XFree(children);
        if (parent == 0 || parent == wroot) break;
        w = parent;
    }
    return w;
}

// Fija la relación transitoria con la ventana activa (Unity) y asegura que el
// diálogo se muestre delante.
static void SetDialogParent(GtkWidget* dialog) {
    if (dialog == NULL) return;

    GdkWindow* gdkwin = gtk_widget_get_window(dialog);
    if (gdkwin != NULL) {
        Window dialog_xid = GDK_WINDOW_XID(gdkwin);
        Window parent_xid = GetActiveTopLevelWindow();
        if (parent_xid != 0 && parent_xid != dialog_xid) {
            Display* dpy = GDK_DISPLAY_XDISPLAY(gdk_display_get_default());
            if (dpy != NULL)
                XSetTransientForHint(dpy, dialog_xid, parent_xid);
        }
    }

    gtk_window_set_keep_above(GTK_WINDOW(dialog), TRUE);
    gtk_window_present(GTK_WINDOW(dialog));
}

const char* DialogOpenFilePanel(const char* title, const char* directory, const char* extension,
                                bool multiselect) {
    return GTKOpenPanel(title, directory, extension, multiselect,
                        GTK_FILE_CHOOSER_ACTION_OPEN);
}

const char* DialogOpenFolderPanel(const char* title, const char* directory, bool multiselect) {
    return GTKOpenPanel(title, directory, "", multiselect,
                        GTK_FILE_CHOOSER_ACTION_SELECT_FOLDER);
}

const char* DialogSaveFilePanel(const char* title, const char* directory, const char* defaultName,
                                const char* filters) {
    return GTKSavePanel(title, directory, defaultName, filters);
}

void DialogOpenFilePanelAsync(const char* title, const char* directory, const char* extension,
                              bool multiselect, callbackFunc cb) {
    // TODO Add async capability
    cb(GTKOpenPanel(title, directory, extension, multiselect,
                        GTK_FILE_CHOOSER_ACTION_OPEN));

}

void DialogOpenFolderPanelAsync(const char* title, const char* directory, bool multiselect,
                                callbackFunc cb) {
    // TODO Add async capability
    cb(GTKOpenPanel(title, directory, "", multiselect,
                        GTK_FILE_CHOOSER_ACTION_SELECT_FOLDER));

}

void DialogSaveFilePanelAsync(const char* title, const char* directory, const char* defaultName,
                              const char* filters, callbackFunc cb) {
    // TODO Add async capability
    cb(GTKSavePanel(title, directory, defaultName, filters));
}

const char*
GTKOpenPanel(const char* title, const char* directory, const char* extensions, bool multiselect,
             GtkFileChooserAction action) {
    char* filename = NULL;
    GtkWidget* dialog;
    gint res;

    dialog = gtk_file_chooser_dialog_new(title,
                                         NULL,
                                         action,
                                         ("_Cancel"),
                                         GTK_RESPONSE_CANCEL,
                                         ("_Open"),
                                         GTK_RESPONSE_ACCEPT,
                                         NULL);
    gtk_file_chooser_set_select_multiple((GtkFileChooser*) dialog, multiselect);
    gtk_file_chooser_set_current_folder((GtkFileChooser*) dialog, directory);

    GTKSetFilters(extensions, dialog);

    // PATCH: realizar la ventana para poder fijar su XID como hija transitoria.
    gtk_widget_realize(dialog);
    SetDialogParent(dialog);

    res = gtk_dialog_run(GTK_DIALOG (dialog));
    if (res == GTK_RESPONSE_ACCEPT) {
        GtkFileChooser* chooser = GTK_FILE_CHOOSER (dialog);

        if (multiselect) {
            GSList* filenamepus = gtk_file_chooser_get_filenames(chooser);

            int nIndex;
            GSList* node;

            char split = (char) 28;
            for (nIndex = 0; (node = g_slist_nth(filenamepus, (guint) nIndex)); nIndex++) {
                if (nIndex == 0) {
                    filename = malloc((strlen(node->data) + 1) * sizeof(char));
                    strcpy(filename, node->data);
                    continue;
                }
                filename = strcat_realloc(filename, &split);
                filename = strcat_realloc(filename, node->data);

                g_free(node->data);
            }
            g_slist_free(filenamepus);
        } else {
            char* name = gtk_file_chooser_get_filename(chooser);
            filename = malloc(strlen(name) * sizeof(char));
            strcpy(filename, name);
            g_free(name);
        }
    } else { // if (res == GTK_RESPONSE_CANCEL) {
        filename = malloc(sizeof(char));
        filename[0] = '\0';
    }

    gtk_widget_destroy(dialog);

    while (gtk_events_pending ())
        gtk_main_iteration ();
    return filename;
}

const char*
GTKSavePanel(const char* title, const char* directory, const char* defaultName, const char* filters) {
    char* filename = NULL;
    GtkWidget *dialog;
    GtkFileChooser *chooser;
    gint res;

    dialog = gtk_file_chooser_dialog_new ("Save File",
                                        NULL,
                                        GTK_FILE_CHOOSER_ACTION_SAVE,
                                        ("_Cancel"),
                                        GTK_RESPONSE_CANCEL,
                                        ("_Save"),
                                        GTK_RESPONSE_ACCEPT,
                                        NULL);
    chooser = GTK_FILE_CHOOSER (dialog);

    gtk_file_chooser_set_do_overwrite_confirmation (chooser, TRUE);
    gtk_file_chooser_set_current_name(chooser, defaultName);
    gtk_file_chooser_set_current_folder(chooser, directory);

    GTKSetFilters(filters, dialog);

    // PATCH: fijar ventana padre transitoria.
    gtk_widget_realize(dialog);
    SetDialogParent(dialog);

    res = gtk_dialog_run (GTK_DIALOG (dialog));
    if (res == GTK_RESPONSE_ACCEPT)
    {
        char* name = gtk_file_chooser_get_filename(chooser);
        filename = malloc(strlen(name) * sizeof(char));
        strcpy(filename, name);
        g_free(name);
    }
    else if (res == GTK_RESPONSE_CANCEL) {
        filename = malloc(sizeof(char));
        filename[0] = '\0';
    }

    gtk_widget_destroy (dialog);

    while (gtk_events_pending ())
        gtk_main_iteration ();
    return filename;
}

void GTKSetFilters(const char* extensions, GtkWidget* dialog) {
    if (extensions == NULL || strlen(extensions) == 0) {
        return;
    }

    //    Image Files;png,jpg,jpeg|Sound Files;mp3,wav|All Files;*
    char* extensions_tok = malloc(sizeof(char) * (1+ strlen(extensions)));
    extensions_tok = strcpy(extensions_tok, extensions);
    char* extensions_tok_beginning = extensions_tok;

    char *extension_filters;
    char *name_or_filters;
    char *ext;
    while ((extension_filters = strtok_r(extensions_tok, "|", &extensions_tok))) {
        puts(extension_filters);
        int i = 0;
        GtkFileFilter* filter = gtk_file_filter_new();
        if (extension_filters[0] == ';') { // no filter name
            ++i;
        }
        while ((name_or_filters = strtok_r(extension_filters, ";", &extension_filters))) {
            puts(name_or_filters);
            if (i == 0) {
                // Filter Name
                gtk_file_filter_set_name(filter, name_or_filters);
            } else {
                // Filter extentions
                while ((ext = strtok_r(name_or_filters, ",", &name_or_filters))) {
                    puts(ext);
                    if (ext[0] == '*') {
                        gtk_file_filter_add_pattern(filter, ext);
                    } else {
                        char* ext_s;// = "*.";
                        ext_s = malloc(3 * sizeof(char));
                        ext_s = strcpy(ext_s, "*.");
                        ext_s = strcat_realloc(ext_s, ext);
                        gtk_file_filter_add_pattern(filter, ext_s);
                        free(ext_s);
                    }
                }
            }
            ++i;
        }
        gtk_file_chooser_add_filter((GtkFileChooser*) dialog, filter);
    }

    free(extensions_tok_beginning);
}
