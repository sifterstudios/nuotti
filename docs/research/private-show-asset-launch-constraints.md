# Private show assets: Norway/EEA launch constraints

Date: 2026-07-31  
Scope: review-ready launch controls for tenant-uploaded Song Package assets and live performance, initially Norway/EEA  
Status: planning research, not legal advice

## Executive conclusion

Nuotti should treat a Workspace's SaaS entitlement, its authority to upload/store each asset, and the event organiser's authority to perform or play material publicly as three independent gates. A paid Nuotti account proves none of the content or performance rights. Launch should therefore require documented customer attestations and provenance, an operational notice-and-takedown path, purpose-specific privacy retention and deletion, and a counsel-approved territory matrix. Private tenant isolation reduces exposure but does not itself supply copyright permission.

The minimum safe product posture is **customer-supplied, private-by-default show material**: no cross-Workspace reuse; no public asset URLs; no Nuotti-curated lyrics, artwork, recordings, backing tracks, or click-derived recordings until separately cleared; and no claim that the service's subscription includes public-performance or content licences.

## Rights and provenance controls

Norway's Copyright Act gives the author exclusive rights to make copies and make a work available to the public, including public performance and transmission; related rights separately protect performers and sound recordings. Private copying is not a general commercial SaaS-upload exception. Treat uploads, server/transient copies, downloads to the Show Agent, projected lyrics/images, playback of recordings, adaptations, and live performance as distinct uses requiring a rights basis. ([Norwegian Copyright Act, Lovdata](https://lovdata.no/dokument/NL/lov/2018-06-15-40))

For Norwegian concerts/events using repertoire it administers, TONO says the organiser must obtain permission before the event, pay remuneration, and report audience, revenue, and repertoire within 14 days. Its licence excludes rights and repertoire TONO does not administer, and specified contextual uses or adaptations can require direct rightsholder consent. The launch workflow must not describe a TONO licence as clearing every asset or use. ([TONO concert/event terms](https://www.tono.no/wp-content/uploads/2024/06/TONOs-vilkar-for-konserter-og-events-revidert-04.06.24.pdf), [TONO concert guidance](https://www.tono.no/kunder/konsert-event/))

Playing a commercial recording may engage rights beyond the composition. Gramo identifies separate remuneration rights for producers and performers when recorded music is publicly performed or communicated; NCB separately licenses some recording/copying uses. Counsel must classify backing tracks, stems, click tracks derived from recordings, and locally cached files rather than assuming the live-performance licence covers reproduction or master rights. ([Gramo 2025 annual report](https://static.gramo.no/files/docs/Gramo-aarsrapport-2025.pdf), [NCB music-copy application](https://www.ncb.dk/ncb/musicapplication))

Required launch controls:

- Each uploaded file has immutable provenance fields: Workspace, uploader, upload time, asset type, source, claimed rights basis, territory, permitted uses, expiry, and supporting-document reference.
- The uploader affirmatively warrants authority for Nuotti to store, process, deliver to its Show Agent, and use the asset as configured. Avoid a single vague “I own this” checkbox.
- Published Song Package Revisions preserve the exact provenance decision for assets owned by Playback Configuration and Hints; replacing one of those assets creates a new reviewable Song Package Revision. Lyric Track provenance follows its independently versioned Lyric Track, and a Session records the exact Lyric Track version it captures.
- Access is Workspace-scoped and least-privilege; object grants are short-lived; raw asset access is audited; deletion propagates to caches and Show Agents.
- A rights status (`pending`, `approved`, `expired`, `disputed`, `blocked`) gates publishing and download. A Nuotti subscription status is stored and enforced separately.
- Preserve setlist/repertoire reporting data needed by the organiser, but do not represent submission to a collecting society as completed unless the product actually integrates and receives evidence.

Lyrics require their own clearance decision. Projecting or reproducing a Lyric Track is not safely inferred from permission to perform the musical work. The same applies to visual hints, album artwork, photographs, and user-created adaptations.

## Notice, takedown, and disputes

Even for private Workspace storage, ship a clearly identified rights-contact channel and an internal case workflow. Norway's e-Commerce Act conditions hosting liability protections in part on acting without undue delay to remove or disable access after the relevant knowledge threshold is met. The EU Digital Services Act establishes a structured, accessible notice-and-action mechanism for hosting services in Article 16; whether and when its EEA incorporation applies to the Norwegian launch must be confirmed by counsel. ([e-Commerce Act §18, Lovdata](https://lovdata.no/dokument/NL/lov/2003-05-23-35/%C2%A718), [Digital Services Act, Regulation (EU) 2022/2065](https://eur-lex.europa.eu/eli/reg/2022/2065/oj))

Operational minimum:

1. Accept notices identifying claimant/contact, right, work, exact asset/location, good-faith basis, and supporting evidence; acknowledge receipt and assign a case ID.
2. Immediately contain credible urgent cases by disabling new grants/downloads while preserving only the restricted evidence needed for the dispute.
3. Notify the Workspace, permit a documented response/counter-evidence process, and record a reasoned decision and timestamps.
4. Remove or restore consistently across object storage, CDN/cache, backups according to documented propagation rules, and paired Show Agents when they reconnect.
5. Maintain repeat-abuse/escalation rules, an emergency route, and counsel escalation for conflicting claims—without promising that Nuotti adjudicates ownership.

## GDPR: roles, transparency, retention, and deletion

Before launch, map purposes and decide whether Nuotti is controller, processor, or both for each dataset. The party determining purposes and means is controller; a processor acts only on documented instructions, and a processor relationship requires an Article 28 agreement. Nuotti is likely controller for accounts, security, billing, entitlement enforcement, abuse/takedown records, and its own service telemetry; the Workspace/Nuotti allocation for Participant display names, answers, scores, Session logs, and uploaded asset metadata requires a fact-specific decision. ([GDPR Articles 4 and 28](https://eur-lex.europa.eu/eli/reg/2016/679/oj), [Datatilsynet role guidance](https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/behandlingsansvarlig-og-databehandler/))

For every purpose, document data categories, subjects, recipients/subprocessors, lawful basis, security, retention, and deletion. GDPR Article 5 requires purpose limitation, minimisation, accuracy, storage limitation, security, and accountability; Articles 6, 13/14, 30 and 32 require a lawful basis, transparent information, processing records where applicable, and risk-appropriate security. Do not use privacy “consent” as a substitute for service terms or rights warranties. ([GDPR](https://eur-lex.europa.eu/eli/reg/2016/679/oj), [Datatilsynet on digital services](https://www.datatilsynet.no/personvern-pa-ulike-omrader/kundehandtering-handel-og-medlemskap/digitale-tjenester-og-forbrukeres-personopplysninger/))

Adopt a reviewed schedule rather than “retain indefinitely”:

| Dataset | Proposed product default (counsel/finance approval required) | Deletion trigger |
|---|---|---|
| Audience Participant identity, answers, score, connection metadata | Session purpose plus a short, explicit operational window | Session expiry or verified request where no exception applies |
| Session audit/security logs | Short security window, separated from product analytics | Schedule expiry; earlier erasure where applicable |
| Workspace account/membership and entitlement | Active contract plus defined post-termination/accounting period | Contract closure plus applicable statutory period |
| Uploaded assets and generated derivatives | While Workspace retains the asset and rights status permits it | Archive/delete command, termination, expiry, or upheld takedown |
| Provenance, licence and takedown evidence | Defined claims/compliance period with restricted access | Limitation/evidence need expires |
| Backups | Fixed rolling expiry; no routine restore of deleted data without reapplying tombstones | Backup rotation |

Article 17 erasure is not absolute: exceptions include compliance with legal obligations and establishment, exercise, or defence of legal claims. A “legal hold” must therefore be case-specific, authorised, documented, access-restricted, reviewed, and released—not a silent indefinite flag. Where accuracy or legality is disputed, Article 18 restriction may require preserving data without ordinary use. Datatilsynet says organisations must facilitate rights requests, generally without charge and within one month, and must also delete proactively in relevant cases. ([GDPR Articles 12, 17 and 18](https://eur-lex.europa.eu/eli/reg/2016/679/oj), [Datatilsynet deletion guidance](https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/retting-og-sletting/))

Deletion must cover primary stores, search indexes, object derivatives, grants, logs where appropriate, and downstream processors; it should issue a tombstone so a backup restore does not resurrect active access. Article 28 contracts must address return/deletion at termination. If support, hosting, telemetry, or other recipients can access personal data outside the EEA, Chapter V needs a valid transfer mechanism and an effective protection assessment; EEA hosting alone does not rule out third-country support access. ([Datatilsynet processor-agreement guidance](https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/hvordan-lage-en-databehandleravtale/), [Datatilsynet international-transfer guidance](https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/overforing-av-personopplysninger-ut-av-eos/))

Run and document a DPIA threshold assessment before launch, especially if the service is marketed to children/young audiences, introduces profiling/rankings at scale, combines persistent identifiers across Sessions, or expands monitoring. Complete a DPIA before processing where Article 35's likely-high-risk threshold is met. ([GDPR Article 35](https://eur-lex.europa.eu/eli/reg/2016/679/oj), [Datatilsynet DPIA checklist](https://www.datatilsynet.no/contentassets/8b767689abb14926af27820c9c2fb89e/sjekkliste-for-dpiafaser.pdf))

## Territory questions counsel must answer

Maintain a versioned country matrix; “EEA” harmonises much privacy law but not every copyright licence, collecting mandate, tariff, consumer rule, or venue obligation.

- In each launch country, who is legally responsible for public-performance permission and reporting: venue, promoter, band/Performer, Workspace customer, or a combination?
- Which collecting society covers composition/performance, which covers performers/producers of recordings, and which covers mechanical reproduction, lyrics/projection, synchronisation, artwork and adaptations?
- Does a society's reciprocal repertoire/mandate cover the exact work, right, venue, event type, audience size, and online/offline use? What requires direct publisher, label, photographer, performer, or author permission?
- Does server storage in one state, Show Agent caching/download in another, and performance in a third create multiple acts/territories requiring separate clearance?
- Are streams, remote/hybrid audiences, recordings, later replays, dynamic video, or lighting/timecode outputs outside an ordinary live-event licence?
- Do consumer, electronic-commerce, platform/hosting, accessibility, child-data, employment/performer, tax/accounting, and limitation-period rules add local requirements?
- Is the DSA incorporated and applicable in the relevant EEA state at launch, and what entity/contact/terms/transparency obligations apply given Nuotti's size and service classification?
- Which language, governing-law, venue, representative, insurance, and local rights-contact requirements apply?

Expansion outside the EEA needs a fresh copyright/licensing and privacy-transfer review; do not reuse a Norway approval as a global flag.

## Ownership and launch evidence

| Decision/control | Responsible owner | Evidence required before launch |
|---|---|---|
| Product rights model and prohibited assets | Product owner + copyright counsel | Signed scope memo; approved terms/warranty; asset-rights matrix |
| Norway performance/licensing workflow | Workspace customer/organiser, verified by Product/Operations | Customer acknowledgement; organiser responsibility stated; TONO/other-society guidance linked; reporting export tested |
| Provenance and access enforcement | Engineering + Security | Threat model; tenant-isolation tests; grant-expiry tests; immutable audit sample |
| Takedown/dispute handling | Operations owner + copyright counsel | Published contact/process; tabletop exercise; response SLA; decision and preservation templates |
| GDPR role/lawful-basis map | Privacy owner/DPO or external privacy counsel | Record of processing; controller/processor matrix; privacy notice; Article 28 DPA and subprocessor list |
| Retention, deletion, legal hold | Privacy + Security + Engineering + counsel | Approved schedule; end-to-end deletion test including backups/Show Agent; hold authorisation/review/release log |
| International transfers | Privacy + Procurement/Security | Data-flow/subprocessor map; region and support-access evidence; adequacy/SCC and transfer assessment where needed |
| DPIA threshold | Privacy owner/DPO | Signed threshold assessment and, if triggered, completed DPIA with mitigations |
| Territory enablement | Legal/Product release owner | Counsel-approved country row; collecting-society contacts/licence boundaries; dated approval and renewal date |

### Go/no-go packet

The release approver should receive one indexed packet containing: product/asset scope; customer terms and privacy notice; rights/provenance schema and sample; country matrix; society/counsel correspondence; data-flow and role map; retention/deletion/hold schedule; processor/subprocessor and transfer records; DPIA threshold result; takedown procedure/tabletop evidence; security/tenant-isolation evidence; and named owners with review/renewal dates. Any unknown is either a documented launch exclusion enforced in product or a no-go item—not an assumption hidden in terms.
