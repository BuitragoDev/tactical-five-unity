cmake_minimum_required(VERSION 3.12)
project(StandaloneFileBrowser C)

set(CMAKE_C_STANDARD 11)

# Usa pkg-config para localizar GTK+ y X11
find_package(PkgConfig REQUIRED)
pkg_check_modules(GTK3 REQUIRED gtk+-3.0)
pkg_check_modules(X11 REQUIRED x11)

include_directories(${GTK3_INCLUDE_DIRS} ${X11_INCLUDE_DIRS})
link_directories(${GTK3_LIBRARY_DIRS} ${X11_LIBRARY_DIRS})
add_definitions(${GTK3_CFLAGS_OTHER})

add_library(${PROJECT_NAME} SHARED library.c library.h)
target_link_libraries(${PROJECT_NAME} ${GTK3_LIBRARIES} ${X11_LIBRARIES})
