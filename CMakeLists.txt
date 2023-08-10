cmake_minimum_required(VERSION 3.0)

project(acme)

set(CMAKE_C_STANDARD 99)

add_executable(acme
    acme.c
    alu.c
    basics.c
    cliargs.c
    cpu.c
    dynabuf.c
    encoding.c
    flow.c
    global.c
    input.c
    label.c
    macro.c
    mnemo.c
    output.c
    platform.c
    section.c
    tree.c
)

target_include_directories(acme PRIVATE ${CMAKE_CURRENT_SOURCE_DIR})