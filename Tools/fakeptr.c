// fakeptr: a real pointer (and keys) for a private headless KWin. It speaks KWin's org_kde_kwin_fake_input protocol, so
// the compositor itself moves its pointer and sends the game wl_pointer enter, motion, leave and button events, exactly
// as a mouse would. Used by Tools/unity.sh pointertest; built by it into Builds/tools/fakeptr.
//
// It only ever connects to the socket named on its command line, and only to one of this repo's private KWins (named
// "laf-..." by Tools/unity.sh), never the desktop's (wayland-0), so it can't move the shared desktop's pointer. The
// nested KWin must be started with KWIN_WAYLAND_NO_PERMISSION_CHECKS=1 (fake input is a restricted interface).
//
// Commands, one per line on stdin:
//   abs X Y         move the pointer to (X, Y) in screen pixels (fractions allowed, as a real mouse gives)
//   rel DX DY       move it by (DX, DY)
//   slide X0 Y0 X1 Y1 STEP MS  from (X0, Y0) to (X1, Y1), STEP pixels every MS milliseconds
//   sync            print "sync" on stdout once everything before it has reached the compositor
//   down [B] | up [B]  press or release a button (default 272, BTN_LEFT)
//   key CODE 1|0    press or release a key (evdev code)
//   axis A V        turn the wheel: axis 0 vertical (V > 0 scrolls down), 1 horizontal
//   wait MS         sleep
// The protocol's interface is declared here by hand (from plasma-wayland-protocols' fake-input.xml, version 4), so no
// generated code is needed: cc -O2 -o fakeptr fakeptr.c -lwayland-client -lm
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <math.h>
#include <wayland-client.h>

static const struct wl_interface *no_types[4] = { NULL, NULL, NULL, NULL };
static const struct wl_message fake_input_requests[] = {
    { "authenticate", "ss", no_types },
    { "pointer_motion", "ff", no_types },
    { "button", "uu", no_types },
    { "axis", "uf", no_types },
    { "touch_down", "2uff", no_types },
    { "touch_motion", "2uff", no_types },
    { "touch_up", "2u", no_types },
    { "touch_cancel", "2", no_types },
    { "touch_frame", "2", no_types },
    { "pointer_motion_absolute", "3ff", no_types },
    { "keyboard_key", "4uu", no_types },
};
static const struct wl_interface fake_input_interface = {
    "org_kde_kwin_fake_input", 4, 11, fake_input_requests, 0, NULL,
};
enum { AUTHENTICATE = 0, POINTER_MOTION = 1, BUTTON = 2, AXIS = 3, POINTER_MOTION_ABSOLUTE = 9, KEYBOARD_KEY = 10 };

static struct wl_proxy *fake;
static uint32_t fake_version;

static void global(void *data, struct wl_registry *reg, uint32_t name, const char *iface, uint32_t version)
{
    (void)data;
    if (strcmp(iface, fake_input_interface.name) == 0) {
        fake_version = version < 4 ? version : 4;
        fake = wl_registry_bind(reg, name, &fake_input_interface, fake_version);
    }
}
static void global_remove(void *data, struct wl_registry *reg, uint32_t name) { (void)data; (void)reg; (void)name; }
static const struct wl_registry_listener registry_listener = { global, global_remove };

int main(int argc, char **argv)
{
    if (argc != 2) { fprintf(stderr, "usage: fakeptr <wayland socket of a private KWin>\n"); return 2; }
    const char *sock = argv[1];
    if (strncmp(sock, "laf-", 4) != 0 || strchr(sock, '/')) {
        fprintf(stderr, "fakeptr: refusing %s: only a private KWin started by Tools/unity.sh (laf-...)\n", sock);
        return 2;
    }
    struct wl_display *dpy = wl_display_connect(sock);
    if (!dpy) { fprintf(stderr, "fakeptr: can't connect to %s\n", sock); return 1; }
    struct wl_registry *reg = wl_display_get_registry(dpy);
    wl_registry_add_listener(reg, &registry_listener, NULL);
    wl_display_roundtrip(dpy);
    if (!fake) { fprintf(stderr, "fakeptr: %s offers no fake input (start KWin with KWIN_WAYLAND_NO_PERMISSION_CHECKS=1)\n", sock); return 1; }
    if (fake_version < 3) { fprintf(stderr, "fakeptr: fake input version %u has no absolute motion\n", fake_version); return 1; }
    wl_proxy_marshal_flags(fake, AUTHENTICATE, NULL, fake_version, 0, "Lost & Found pointertest", "drives the test pointer");
    wl_display_roundtrip(dpy);
    fprintf(stderr, "fakeptr: connected to %s (fake input v%u)\n", sock, fake_version);

    char line[256];
    while (fgets(line, sizeof line, stdin)) {
        double a = 0, b = 0, c = 0, d = 0, step = 0, ms = 0;
        unsigned u = 0, s = 0;
        if (sscanf(line, "slide %lf %lf %lf %lf %lf %lf", &a, &b, &c, &d, &step, &ms) == 6 && step > 0) {
            double len = sqrt((c - a) * (c - a) + (d - b) * (d - b));
            int n = (int)ceil(len / step);
            for (int i = 1; i <= n; i++) {
                double k = i == n ? 1.0 : i * step / len;
                wl_proxy_marshal_flags(fake, POINTER_MOTION_ABSOLUTE, NULL, fake_version, 0,
                                       wl_fixed_from_double(a + (c - a) * k), wl_fixed_from_double(b + (d - b) * k));
                if (wl_display_roundtrip(dpy) < 0) { fprintf(stderr, "fakeptr: the compositor went away\n"); return 1; }
                usleep((useconds_t)(ms * 1000.0));
            }
            continue;
        }
        else if (strncmp(line, "sync", 4) == 0) { wl_display_roundtrip(dpy); printf("sync\n"); fflush(stdout); continue; }
        else if (sscanf(line, "abs %lf %lf", &a, &b) == 2)
            wl_proxy_marshal_flags(fake, POINTER_MOTION_ABSOLUTE, NULL, fake_version, 0, wl_fixed_from_double(a), wl_fixed_from_double(b));
        else if (sscanf(line, "rel %lf %lf", &a, &b) == 2)
            wl_proxy_marshal_flags(fake, POINTER_MOTION, NULL, fake_version, 0, wl_fixed_from_double(a), wl_fixed_from_double(b));
        else if (strncmp(line, "down", 4) == 0 || strncmp(line, "up", 2) == 0) {
            int down = line[0] == 'd';
            if (sscanf(line + (down ? 4 : 2), "%u", &u) != 1) u = 272;
            wl_proxy_marshal_flags(fake, BUTTON, NULL, fake_version, 0, u, down ? 1u : 0u);
        }
        else if (sscanf(line, "axis %u %lf", &u, &a) == 2)
            wl_proxy_marshal_flags(fake, AXIS, NULL, fake_version, 0, u, wl_fixed_from_double(a));
        else if (sscanf(line, "key %u %u", &u, &s) == 2 && fake_version >= 4)
            wl_proxy_marshal_flags(fake, KEYBOARD_KEY, NULL, fake_version, 0, u, s);
        else if (sscanf(line, "wait %lf", &a) == 1) { wl_display_flush(dpy); usleep((useconds_t)(a * 1000.0)); continue; }
        else continue;
        if (wl_display_roundtrip(dpy) < 0) { fprintf(stderr, "fakeptr: the compositor went away\n"); return 1; }
    }
    wl_display_disconnect(dpy);
    return 0;
}
