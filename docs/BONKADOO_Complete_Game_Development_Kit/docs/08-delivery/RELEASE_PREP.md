# Release preparation (drafts — require legal, store and human sign-off)

## Release checklist status (ROADMAP)
| Item | Status | Owner gate |
|---|---|---|
| Title/trademark, store name clearance (D01) | Not started | Human / legal |
| Icon, screenshots, trailer | Not started; dev screenshots only (placeholder art) | Art direction |
| Privacy policy, consent, SDK declarations | Draft below; final depends on ad SDK chosen (D12) | Legal |
| Age rating questionnaire | Draft answers below | Human |
| VI/EN review | All copy is draft; editorial review required | Writers / native reviewers |
| Restore purchases | Implemented (Unity IAP; iOS restore button in Settings) | Store sandbox test |
| Offline mode | No network code in gameplay; ads/IAP failures are non-blocking (tests) | Device airplane-mode test (TC-007) |
| Crash-free sessions | No crash reporting integrated | Decision + human |
| Hardware matrix | Not run | QA with devices (T020) |
| Content licensing | All assets original/procedural; packages MIT (Yarn) or Unity (Newtonsoft, IAP, URP, Input System) | Legal confirm |
| Accessibility | Text scale, reduced motion, captions, drag alternative, labels on every hotspot, safe area | Device review |
| Store human approval | Not submitted (irreversible publishing requires explicit approval) | Human |
| Signing | Not configured; no keystore/certificates in repo by design | Human (secrets in CI) |

## Privacy policy (draft)
BONKADOO! / NGƯỢC ĐỜI does not require an account and stores your progress and settings only on your device.
We do not collect your name, contacts, location or dialogue choices. If advertising is enabled in your region,
our ad partner may process device identifiers and usage data to show and measure ads, subject to your consent
choices (personalised ads are shown only with consent; ATT on iOS). Purchases are processed by Apple or Google;
we receive only confirmation of the "Remove Ads" entitlement. Deleting the app deletes local data. Contact: [to be provided].
*Finalize after the ad SDK and analytics decisions; legal review required.*

## Store listing (draft)
**EN short:** Get fired. Get out. Outsmart a city that runs on absurd rules.
**EN long:** Tí has been fired for being too efficient, but fired employees cannot receive documents, so he cannot
leave. With Bốp, a sarcastic floating cube, bend stamps, forms, queues and witnesses to escape the office, prove you
exist, save a bread stall and confront LOGIC, the polite system rewriting everyone's records. 45 puzzles with two
solutions each, a world that remembers how you solved things, and four endings. Free to play to the end; small banner
ads and a one-time Remove Ads purchase. Fully playable offline. Vietnamese and English.
**VI ngắn:** Bị sa thải. Thoát ra. Lách luật trong một thành phố vận hành bằng những quy định vô lý.
**VI dài:** Tí bị sa thải vì làm việc quá hiệu quả, nhưng nhân viên bị sa thải không được nhận giấy tờ, nên anh
không thể ra về. Cùng Bốp, khối vuông bay hay mỉa mai, hãy uốn cong con dấu, biểu mẫu, hàng chờ và nhân chứng để
thoát khỏi văn phòng, chứng minh mình tồn tại, cứu sạp bánh mì và đối mặt LOGIC. 45 câu đố, mỗi câu hai cách giải,
một thế giới ghi nhớ cách bạn giải, và bốn kết thúc. Miễn phí đến cuối; quảng cáo banner nhỏ và một lần mua Gỡ
quảng cáo. Chơi offline hoàn toàn. Tiếng Việt và tiếng Anh.

## Age rating (draft answers)
No violence, gore, sexual content, gambling, user-generated content or chat. Mild comic mischief (bureaucratic satire).
Ads present (banner, interstitial at scene breaks). In-app purchase present (one non-consumable).
Target audience 13+ suggested pending child-directed review (ADS_AND_IAP consent rules).
