# Nuotti Audience - Modernized UI

## Overview

The Nuotti Audience application has been completely refreshed with a modern, playful design inspired by Kahoot and Bandle. The application now features a mobile-first responsive layout with comprehensive light/dark mode support.

## ✨ Key Features

### 🎨 Theme System
- **Automatic theme detection**: Respects system dark/light mode preferences
- **User override**: Toggle between light and dark mode manually
- **Persistent preferences**: Theme choice saved in local storage
- **Smooth transitions**: All theme changes animate smoothly
- **Kahoot/Bandle-inspired color palette**:
  - Primary: Vibrant Orange (#FF6B35)
  - Secondary: Deep Blue (#004E89)
  - Tertiary: Teal (#1B9AAA)
  - Playful gradients and vibrant accents throughout

### 📱 Mobile-First Design
- Fully responsive layout optimized for mobile devices
- Touch-friendly buttons and controls
- Adaptive grid layouts that reflow on different screen sizes
- Mobile-optimized navigation drawer

### 🎵 Session Management
- **Easy join flow**: Enter session code and optional name
- **Session code display**: Always visible in app bar when connected
- **Participant tracking**: See who else is in the session
- **Real-time updates**: Participants list updates as people join

### 🎮 Waiting Room & Gameplay
- **Lobby view**: Shows session code, participants, and helpful tips
- **Animated UI**: Playful bounce-in and slide-up animations
- **Question display**: Clean, colorful answer buttons inspired by Kahoot
- **Answer feedback**: Visual confirmation when answer is submitted
- **Multiple question options**: Each option has unique color and icon

### 🔍 Song Search Component
- **Real-time search**: Autocomplete with immediate feedback
- **Fuzzy matching**: Finds songs by title or artist
- **Snappy performance**: Debounced input with 150ms delay
- **Beautiful results**: Rich list items with icons and styling
- **Keyboard navigation**: Full keyboard support for accessibility

## 🏗️ Architecture

### New Services

#### `ThemeService.cs`
Manages light/dark mode with:
- System preference detection via JavaScript interop
- User preference storage in localStorage
- Event-driven theme change notifications
- Custom MudBlazor theme with playful color palette

### New Components

#### `SongSearchBar.razor`
Reusable autocomplete search component for songs:
- Accepts list of `SongRef` objects
- Real-time filtering and sorting
- Customizable appearance (variant, density, max results)
- Rich item templates with icons

#### `ParticipantsList.razor`
Displays session participants in a grid:
- Colorful avatars with initials
- Animated entries (bounce-in effect)
- Responsive grid layout
- Hover effects for interactivity

### Enhanced Services

#### `AudienceHubClient.cs`
Extended with participant tracking:
- `Participants` property (read-only list)
- `ParticipantsChanged` event
- Automatic participant list updates on join events

## 🎨 Design System

### Colors (Light Mode)
- Primary: #FF6B35 (Vibrant Orange)
- Secondary: #004E89 (Deep Blue)
- Tertiary: #1B9AAA (Teal)
- Background: #FAFAFA (Light Gray)
- Surface: #FFFFFF (White)

### Colors (Dark Mode)
- Primary: #FF8C61 (Softer Orange)
- Secondary: #2E7DAF (Lighter Blue)
- Tertiary: #48C9B0 (Brighter Teal)
- Background: #0A0E27 (Very Dark Blue-Black)
- Surface: #151B3B (Dark Blue)

### Typography
- Font Family: Roboto, Helvetica, Arial, sans-serif
- Bold headings (600-800 weight)
- Responsive font sizes
- Uppercase styling for buttons

### Spacing & Layout
- Border Radius: 12px (default)
- Cards: 16-20px border radius
- Consistent padding: 16-24px
- Grid gaps: 12-16px

## 🎬 Animations

All animations are CSS-based and performant:

- **bounce-in**: Playful entrance animation with elastic effect
- **slide-up**: Smooth upward entrance from bottom
- **pulse**: Subtle pulsing for waiting states
- **float**: Gentle floating animation for hero icons
- **hover effects**: Lift and shadow on interactive elements

## 📱 Responsive Breakpoints

- Mobile: < 600px
- Tablet: 600px - 960px
- Desktop: > 960px

All layouts adapt smoothly across these breakpoints.

## 🚀 Getting Started

### Running the Application

```bash
cd Nuotti.Audience
dotnet run
```

The application will be available at `https://localhost:5001` (or the configured port).

### Joining a Session

1. Navigate to the home page
2. Enter the session code (shown on the performer's screen)
3. Optionally enter your name
4. Click "Join Session"
5. Wait in the lobby for the quiz to start

### Playing the Quiz

1. When a question appears, read it carefully
2. Select one of the colored answer buttons
3. Wait for the next question
4. Compete with other participants for the high score!

## 🛠️ Development

### Key Files

- **Theme**: `Services/ThemeService.cs`, `wwwroot/js/theme.js`
- **Styling**: `wwwroot/css/app.css`
- **Components**: `Components/SongSearchBar.razor`, `Components/ParticipantsList.razor`
- **Pages**: `Pages/Home.razor`, `Pages/Question.razor`
- **Layout**: `Layout/MainLayout.razor`

### Adding New Features

The application is built with extensibility in mind:

1. **New Components**: Add to `Components/` directory
2. **New Services**: Add to `Services/` directory and register in `Program.cs`
3. **Styling**: Add custom CSS to `wwwroot/css/app.css` or component-level `<style>` blocks
4. **Theme Customization**: Modify `ThemeService.GetTheme()` method

## 🎯 Design Principles

1. **Mobile First**: Design and test on mobile before desktop
2. **Playful but Professional**: Fun animations without sacrificing usability
3. **Accessible**: Proper contrast ratios, keyboard navigation, semantic HTML
4. **Performant**: CSS animations, debounced inputs, efficient rendering
5. **Consistent**: Unified design language across all screens

## 📚 Dependencies

- **MudBlazor 8.12.0**: UI component library
- **Microsoft.AspNetCore.SignalR.Client**: Real-time communication
- **Nuotti.Contracts**: Shared contracts for messages and models

## 🔮 Future Enhancements

Potential improvements for future iterations:

- [ ] Session discovery/browsing (public sessions)
- [ ] QR code generation for session sharing
- [ ] Score display and leaderboard
- [ ] Sound effects for interactions
- [ ] Confetti animation for correct answers
- [ ] Player avatar customization
- [ ] Session history and statistics
- [ ] Progressive Web App (PWA) support for installation

## 📝 Notes

- The application is a Blazor WebAssembly SPA
- All state management is client-side
- SignalR connection is established on session join
- Theme preference persists across sessions
- Dev tools can be revealed for testing audio playback

---

**Built with ❤️ using Blazor, MudBlazor, and modern web standards**

