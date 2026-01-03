# Scrappy Bot - Development Roadmap

## Phase 1: The Core Foundation (Stability & Basic Utility) ✅
**Goal:** Build a stable, responsive bot with essential information tools.
- [x] **Server Info Command:** Display member counts, region, owner, and creation date.
- [x] **User Info Command:** Show join date, roles, and account age.
- [x] **Avatar Fetcher:** Simple command to get user PFPs.
- [x] **Auto-Role:** Automatically assign a "Member" role on join.
- [x] **Welcome Messages:** Basic configurable text channel messages. (Goodbye pending)

## Phase 2: Moderation & Protection (The "Workhorse") ✅
**Goal:** Provide essential moderation tools to replace other bots.
- [x] **Kick & Ban:** Standard removal commands.
- [x] **Mute/Timeout:** Integration with Discord's native timeout API.
- [x] **Purge/Clear:** Bulk message deletion.
- [x] **Mod Logs:** A dedicated channel where the bot logs all actions.
- [x] **Word Filter (Auto-Mod):** Basic banned words list.
- [x] **Warning System:** Track strikes against users in the database.

## Phase 3: Engagement & Community Tools
**Goal:** Features that regular members interact with daily.
- [ ] **Reaction-based Polls:** Simple command to start a poll.
- [ ] **Reaction Roles (Basic):** Allow users to self-assign roles via reactions.
- [ ] **Reminder System:** `!remindme` functionality.
- [ ] **Social Media Feeds:** Notifications for Twitter/YouTube.

## Phase 4: Advanced Data & Analytics (The "Premium" Hook)
**Goal:** Deep insights that admins will pay for.
- [ ] **Deep Engagement Analytics:** Dashboard graphs for "Active Hours" and "User Growth".
- [ ] **Extended Data Retention:** Keep logs for > 90 days.
- [ ] **High-Fidelity Transcripts:** Exportable HTML logs of ticket/channel history.

## Phase 5: Enterprise Customization
**Goal:** White-labeling for high-tier clients.
- [ ] **Custom Bot Branding:** Allow servers to set the bot's name/avatar.
- [ ] **Custom Embed Styling:** Global config for embed colors and footers.
- [ ] **Custom Dashboard Domain:** Access dashboard via `manage.theirserver.com`.