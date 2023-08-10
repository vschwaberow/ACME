# FILEPATH: /home/volker/Sources/ACME/CMakeLists.txt

# Release build
set(CMAKE_BUILD_TYPE Release)
add_executable(acme
    src/acme.c
    src/alu.c
    src/basics.c
    src/cliargs.c
    src/cpu.c
    src/dynabuf.c
    src/encoding.c
    src/flow.c
    src/global.c
    src/input.c
    src/label.c
    src/macro.c
    src/mnemo.c
    src/output.c
    src/platform.c
    src/section.c
    src/tree.c
    src/PDB.cpp
    src/WrapPython.cpp
)
target_compile_options(acme PRIVATE -Wall -std=c99 -O3 -D_GNU_SOURCE)

# Debug build
set(CMAKE_BUILD_TYPE Debug)
add_executable(acme_debug
    src/acme.c
    src/alu.c
    src/basics.c
    src/cliargs.c
    src/cpu.c
    src/dynabuf.c
    src/encoding.c
    src/flow.c
    src/global.c
    src/input.c
    src/label.c
    src/macro.c
    src/mnemo.c
    src/output.c
    src/platform.c
    src/section.c
    src/tree.c
    src/PDB.cpp
    src/WrapPython.cpp
)
target_compile_options(acme_debug PRIVATE -Wall -std=c99 -g -O0 -DDEBUG -D_GNU_SOURCE)
# Find Python
find_package(Python REQUIRED COMPONENTS Interpreter Development)

# Add include directories
include_directories(${Python_INCLUDE_DIRS})

# Add libraries
target_link_libraries(acme ${Python_LIBRARIES} -lc)
target_link_libraries(acme_debug PRIVATE ${Python_LIBRARIES} -lc)
