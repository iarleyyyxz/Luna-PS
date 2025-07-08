# 🎮 Luna‑PS — Emulador de PlayStation 1

![MIT License](https://img.shields.io/github/license/iarleyyyxz/Luna-PS?style=flat-square)
![Build](https://img.shields.io/badge/build-passing-brightgreen?style=flat-square)
![Platform](https://img.shields.io/badge/platform-Windows%20%7C%20Linux-blue?style=flat-square)


![OpenGL](https://img.shields.io/badge/OpenGL-Enabled-5586A4?style=for-the-badge&logo=opengl&logoColor=white)
![Direct3D11](https://img.shields.io/badge/Direct3D11-Enabled-0078D6?style=for-the-badge&logo=windows&logoColor=white)
![Vulkan](https://img.shields.io/badge/Vulkan-Experimental-BB2222?style=for-the-badge&logo=vulkan&logoColor=white)
![Software Render](https://img.shields.io/badge/Software_Render-Debug_Only-888888?style=for-the-badge&logo=circle&logoColor=white)


> Um emulador open-source de **PlayStation 1** feito para estudo, performance e compatibilidade, com renderização em **OpenGL**, **Direct3D 11**, **Vulkan** e modo **Software**.

---

## ⏳ Progresso de Implementação

---

## 🚀 Highlights

- ✅ **CPU completa** (MIPS R3000A) com exceções, pipeline e manipulação de registros  
- ✅ **GPU** com execução parcial de comandos GP0 (polígonos, sprites, tiles)  
- ⚙️ **GTE básico**: operações vetoriais e transformações geométricas  
- 🖥️ **Renderização**:  
  - ✅ OpenGL (cross-platform)  
  - ✅ Direct3D 11 (Windows)  
  - ✅ Vulkan (experimental)  
  - ✅ Modo software puro para debugging  
- 🧠 BIOS real suportada (ex: SCPH1001)  
- 🎮 Suporte a entrada via teclado, XInput e SDL Gamepads  
- 🧰 CLI avançado com flags para depuração

---

## 📸 Prévia

> ⚠️ Capturas reais serão adicionadas assim que possível.
>
> 
---

## 📦 Requisitos

| Sistema       | Suporte     |
|---------------|-------------|
| Windows 10+   | ✅ Total     |
| Linux (x64)   | ✅ Total     |
| macOS         | ⚠️ Parcial (OpenGL apenas)  
| Android       | 🚫 Planejado

> Compilado com **C++17**, **CMake ≥ 3.15**, e **SDL2**. Requer drivers de GPU atualizados para renderização.

---

## 🛠️ Como Compilar

```bash
git clone https://github.com/iarleyyyxz/Luna-PS.git
cd Luna-PS
mkdir build && cd build
cmake .. -DCMAKE_BUILD_TYPE=Release
make -j$(nproc)



