# WPF Components (Banking / Self-Service UI Kit)

A collection of reusable **WPF components** designed for **banking-style applications** such as ATM, kiosk, branch self-service and internal enterprise screens.

The focus is on:
- **Touch-first UX**
- **Consistent navigation & screen hosting**
- **Theming (Dark/Light + brand colors)**
- **Timeout / interruption flows**
- **High readability & maintainable architecture**

> Built and used in real-world banking-style UI scenarios.  
> This repo aims to provide a clean, reusable toolkit that can be embedded into other WPF/WinForms host applications.

---

## ✨ Highlights

- **Modular UserControls** ready to drop into WPF screens
- **Screen Container** approach (screen hosting, popups, overlays)
- **Theme-ready** styles & resource dictionaries
- **ATM/Kiosk-friendly patterns** (timeouts, confirmations, retry/exit flows)
- **Performance-aware** rendering for long-running kiosk/terminal apps

---

## 🧩 Components

> The list below is a template — update names to match your repository structure.

### UI Controls
- **StreamBorderControl**  
  Animated “snake/stream” border effect for focus / loading / highlight states.
- **CountdownTimer**  
  Timeout-friendly timer UI (auto start, pause/resume, event callbacks).
- **QrCodeControl**  
  Generates and displays QR codes with custom colors (works well for payment / login flows).
- **PinPad / KeyPad (Planned / WIP)**  
  Dynamic keypad layouts and secure input UX patterns.

### Screen & Flow Utilities
- **ScreenContainer**  
  Hosts screens, overlays, and popups in a consistent layout.
- **Message / Dialog Screens**  
  Info / Warning / Error / Confirmation flows with consistent styling.
- **Timeout Screen Base (Pattern)**  
  Idle timeout → exit / retry / continue patterns used in kiosk/ATM UX.

---

## 📸 Screenshots / Demo

Add screenshots here:

- <img width="320" height="327" alt="image" src="https://github.com/user-attachments/assets/667c6815-df97-4a10-8fba-69f2a880781f" />
- <img width="515" height="363" alt="image" src="https://github.com/user-attachments/assets/ce23f818-2442-44d1-870a-85a13d989edc" />
- <img width="1159" height="317" alt="image" src="https://github.com/user-attachments/assets/3c86a190-a716-4647-9347-c99c4366fdb3" />

> Tip: GIFs increase trust and make the repo instantly understandable.

---

## ✅ Requirements

- **.NET Framework 4.8** (or compatible)
- **WPF**
- Optional (for some controls):
  - `ZXing` / `ZXing.Net` for QR generation

---

## 🚀 Getting Started

### Option A — Copy Source (fastest)
Clone the repo and reference the project(s) directly:

1. Clone:
   ```bash
   git clone https://github.com/alicanyilmazz/WPF-Components.git








