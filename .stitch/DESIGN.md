# Design System: Yeoyang School Office

Project ID: 16363322754853011110 (캠퍼스 링크 메신저). This is a local review brief. Existing Stitch systems are EduLink Modern Desktop and CampusTalk; this file has not been uploaded as a new design system.

## 1. Visual Theme & Atmosphere

A calm Korean school workspace. Precise typography, restrained elevation, compact information, and subtle frosted surfaces. There are three separate products: the native Windows WPF PC messenger, the teacher school app, and the student/parent school app. Both mobile apps are native .NET MAUI applications for Android and iOS. They share authorized school information through the existing servers. Designs are visual references for native implementation, not a proposal to replace these applications with a website. Do not label the student/parent app as a family app.

Glass means translucent cards above the app's own background, a fine light edge, and a soft shadow. Keep reading surfaces nearly opaque. Never blur text or place personal information over desktop wallpaper. Provide an opaque alternative and respect reduced transparency and high contrast. No animated background blur.

## 2. Color Palette & Roles

Baseline: Yeoyang Blue. Ink #203048; secondary ink #5A6B81; dividers #DCE3ED; canvas #F3F6FB; card #FFFFFF with optional 96% opacity. Accent #245BC6; accent wash #E7EFFB. Meaningful text and indicators must retain readable contrast in opaque and glass modes. Error and success states require text or an icon as well as color.

Desktop user-selectable palettes, preserving the same hierarchy:

| Name | Accent | Wash |
| --- | --- | --- |
| 여양 블루 | #245BC6 | #E7EFFB |
| 숲 그린 | #17745D | #E3F1EB |
| 라벤더 | #7050B0 | #EEE8F7 |
| 로즈 | #AD4865 | #F7E7ED |
| 슬레이트 | #506277 | #E9EDF2 |

Do not encode online status, overdue status, or selection solely through the chosen accent. Each palette needs visible selected and focused states. Desktop appearance is a local user preference.

## 3. Typography Rules

Korean-first labels and synthetic Korean sample content. Noto Sans for Stitch rendering; platform system fonts in native implementation, including Malgun Gothic on Windows. Desktop primary text 12–14px, section titles 14–16px. Mobile body 14–16px and main headings 24–25px. Medium weight for labels and semibold for hierarchy. Comfortable Korean line spacing. Avoid tiny uppercase labels, decorative text, and excessive bold.

Long names and titles wrap where useful; clipping must not hide the action or audience. Dynamic font size must preserve content order and allow scrolling.

## 4. Component Stylings

Compact desktop panels with roughly 10px corner radii; mobile cards about 20px. Thin dividers and modest shadows. Icons follow one simple line system. Buttons identify the action with a short Korean label; primary emphasis is scarce. Read/unread, online/offline, and changed timetable entries have explicit labels. Inputs always have visible labels and validation near the field. Time controls use Korean time (KST) and 24-hour values.

Mobile touch targets at least 44–48px; desktop supports keyboard focus and context menus. Floating mobile navigation respects platform safe areas. Glass is decorative, not required to read or navigate.

## 5. Layout Principles

Desktop default window is 450×820; preserve the small organization tree and dense work flow with natural scrolling. Mobile uses one clear column and short grouped cards; related actions can share a row if text remains readable. Keep the current home/messages-or-news/profile navigation. Timetable, school calendar, room reservation, messages, notices, and child selection use the existing permission model.

Show realistic loading, empty, disconnected, and form-error states. Keep drafts when a request fails. No invented controls for unavailable meals, closed-app push delivery, or mobile real-time chat. Never expose out-of-scope class notices in a design example.
