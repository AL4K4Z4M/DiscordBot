# Scrappy Project Guidelines

This document serves as the comprehensive guide for maintaining the Scrappy Dashboard. Adherence to these rules ensures code quality, consistency, and scalability.

## 1. Styling & Design System

### Centralized Design Tokens
All colors, fonts, and border-radius settings are defined in:
`wwwroot/js/theme-config.js`

*   **Rule:** Never hardcode HEX/RGB values for theme colors. Use Tailwind keys (e.g., `text-primary`, `bg-surface-dark`).
*   **Maintenance:** To change the app's look, modify `theme-config.js` only.

### CSS Architecture
All custom CSS lives in `wwwroot/styles/`:
*   `base.css`: Resets and global utilities.
*   `animations.css`: Keyframes and motion.
*   `components.css`: Styling for framework-specific elements.

### The "No Exceptions" Policy
*   **NO** `<style>` blocks in Razor files.
*   **NO** Razor scoped CSS (`*.razor.css`).
*   **NO** static inline `style="..."` attributes.
    *   *Exception:* Dynamic data-driven values (e.g., role colors, chart heights).

---

## 2. Architecture & Code Structure

### Project Layout
*   `Pages/`: Top-level routeable components (e.g., `ServerDashboard.razor`).
*   `Shared/`: Reusable UI components (e.g., `MainLayout.razor`, headers).
*   `Services/`: Business logic and data access (e.g., `BotService.cs`).

### Component Design
*   **Single Responsibility:** Each component should do one thing well. Break complex dashboards into smaller sub-components (e.g., `ServerStatsCard.razor`).
*   **Parameters:** Use `[Parameter]` for data passing. Avoid tight coupling to global state within child components.

### State Management
*   **Services:** Use injected services (Singletons/Scoped) for sharing state between components (e.g., `DiscordSocketClient`).
*   **Events:** Utilize C# events (e.g., `DiscordClient.Ready`) to trigger UI updates (`InvokeAsync(StateHasChanged)`).

---

## 3. User Experience (UX)

### Navigation
*   **Breadcrumbs/Back Buttons:** Always provide a clear path back to the previous screen (e.g., "Back to Servers").
*   **Loading States:** Never show a broken or empty UI. Use loading spinners or skeletons while data is fetching.

### Error Handling
*   **Graceful Degradation:** If a feature fails (e.g., API timeout), show a user-friendly error message, not a stack trace.
*   **Visual Feedback:** Use toast notifications or alert boxes for success/failure feedback.

---

## 4. Development Workflow

### Clean Code
*   **Namespaces:** Keep `using` directives clean and organized.
*   **Formatting:** Follow standard C# and Razor formatting conventions.
*   **Comments:** Comment *why*, not *what*. Explain complex logic or hacks.

### Security
*   **Authentication:** Always use `<AuthorizeView>` for protected content.
*   **Secrets:** NEVER commit API keys or tokens to Git. Use `appsettings.json` (git-ignored) or Environment Variables.