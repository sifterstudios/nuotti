# Private show asset launch constraints

_Product-planning research for Nuotti issue
[#240](https://github.com/sifterstudios/nuotti/issues/240), researched 31 July
2026. This is not legal advice. Counsel should confirm the launch design,
contracts, and licensing position for every territory in which Nuotti operates._

## Executive answer

Nuotti can reduce risk by supplying only a factual Song Catalog and making each
Workspace responsible for privately uploaded backing tracks, click tracks,
images, and lyrics, but calling those files "private" does not itself license
copying, cloud storage, projection, performance, or transmission. The product
should therefore launch with:

1. a rights warranty and narrowly scoped licence from each Workspace;
2. tenant-isolated, access-controlled storage with no cross-Workspace sharing;
3. a documented notice-and-action process, repeat-infringer policy, evidence
   preservation path, and counter-notice/escalation path;
4. explicit retention schedules and self-service deletion for both show assets
   and participant data;
5. a recorded privacy-role analysis, privacy notice, processor contracts,
   lawful bases, and EEA transfer controls; and
6. a pre-launch legal review covering live-performance, lyric-display,
   reproduction/synchronisation, and sound-recording rights in each launch
   territory.

The safest MVP boundary is not to sell, seed, inspect for reuse, or share
copyrighted show assets. It should not promise that a venue's public-performance
licence covers Nuotti's separate reproduction, lyric display, cloud-storage, or
transmission acts.

## Why the asset model still needs licences

### Norway

Norway's Copyright Act gives an author the exclusive right to make copies and
make a work available to the public; it treats performance/display outside the
private sphere as making available. Its private-copying exception applies only
when use is not commercial and permits only individual copies for private use.
Commercial bands uploading assets to a SaaS service for an audience show should
therefore not be designed around the private-copy exception
([Copyright Act §§ 3 and 26, Lovdata](https://lovdata.no/dokument/NL/lov/2018-06-15-40)).

A single song can involve separate rights. In particular, a musical composition
(including lyrics) and a sound recording are distinct protected works; the same
separation is also stated plainly by the U.S. Copyright Office
([Musical Compositions and Sound Recordings](https://www.copyright.gov/register/pa-sr.html)).
Product terms should require the Workspace to hold permissions broad enough for
each actual operation: upload/reproduction, server storage and band-computer
caching, projector display of lyrics/images, live performance, and any network
delivery to other devices. A click track created by the band may be band-owned,
while a backing track, composition, lyrics, or image may have different owners.

The service should collect a rights attestation at upload and retain which
Workspace member supplied each asset, but an attestation is risk allocation—not
proof of a licence. Do not make uploaded assets discoverable or reusable across
Workspaces.

### European Union

For EU launches, the InfoSoc Directive reserves reproduction and
communication/making-available rights and requires Member States to protect
technological measures. Exceptions are specific rather than a general
"private show" permission
([Directive 2001/29/EC, Articles 2, 3, 5 and 6](https://eur-lex.europa.eu/eli/dir/2001/29/oj)).
Implementation and collective-licensing practice vary by Member State, so an
EU-wide launch needs territory-by-territory advice.

### United States

U.S. copyright likewise separates the musical composition (music and words)
from its sound recording
([U.S. Copyright Office](https://www.copyright.gov/register/pa-sr.html)).
The Copyright Act's owner rights include reproduction, derivative works,
distribution, public performance and public display, with an additional
digital-audio-transmission right for sound recordings
([17 U.S.C. §106](https://www.copyright.gov/title17/92chap1.html#106)).
Consequently, a venue or performing-rights-organisation licence should not be
assumed to authorize cloud copies, cached copies, lyric display, adaptations,
or every transmission. Obtain U.S.-specific advice before launch.

## Notice, takedown, and intermediary rules

### Norway now; DSA later

Norway's current intermediary-liability rules are in the E-Commerce Act. A
hosting provider's liability limitation depends on lacking knowledge of
unlawful activity and acting quickly to remove or disable access after obtaining
knowledge
([E-Commerce Act §18, Lovdata](https://lovdata.no/dokument/NL/lov/2003-05-23-35)).

As of this note, Norway's government says the EU Digital Services Act (DSA) is
EEA-relevant but has not yet been incorporated into the EEA Agreement
([Prop. 41 LS (2025–2026), section 13](https://www.regjeringen.no/no/dokumenter/prop.-41-ls-20252026/id3154279/?ch=13)).
Nuotti should nevertheless make its design DSA-ready because direct EU service
and later Norwegian implementation can bring it into scope. For hosting
services, the DSA requires an easy electronic notice mechanism, timely and
diligent decisions, a statement of reasons when access is restricted, and
reports of suspected serious criminal offences; exemptions exist for qualifying
micro/small enterprises for some—not all—duties
([DSA Articles 6, 16, 17, 18 and 19](https://eur-lex.europa.eu/eli/reg/2022/2065/oj)).

MVP controls should include:

- a public infringement-report address/form accepting exact asset and
  Workspace/Session identifiers, claimant identity, basis, and good-faith
  statement;
- immediate quarantine/disable capability scoped to the identified asset;
- notice, decision, action, and restoration audit records;
- notification to the Workspace with a route to contest or supply a licence;
- a documented repeat-infringer escalation/termination policy;
- an urgent escalation path for court/authority orders and credible safety
  issues; and
- preservation of the minimum evidence needed for disputes, kept separately
  from normal asset retention.

### United States

If Nuotti serves the U.S., it should decide deliberately whether to seek the
17 U.S.C. §512(c) safe harbour for storage at a user's direction. The Copyright
Office says eligibility includes registering a designated agent, posting the
agent publicly, and expeditiously removing or disabling access after a compliant
notice
([Copyright Office online-service-provider guidance](https://www.copyright.gov/onlinesp/)).
Section 512 also requires a reasonably implemented repeat-infringer policy and
sets notice and counter-notice requirements
([17 U.S.C. §512](https://www.copyright.gov/title17/92chap5.html#512)).
The U.S. workflow should therefore include valid-notice checks, takedown,
subscriber notice, counter-notice, the statutory restoration waiting process,
and escalation when the claimant files suit. These U.S. mechanics should not be
presented as the legal procedure for Norway or the EU.

## Privacy, retention, and deletion (Norway/EEA)

Norway incorporated the GDPR through the Personal Data Act
([Personal Data Act and GDPR, Lovdata](https://lovdata.no/dokument/NL/lov/2018-06-15-38)).
Display names, device/session identifiers, answers, timestamps, scores, IP
addresses, moderation records, member accounts, and uploader/audit records can
all be personal data when they relate to an identifiable person.

Before implementation, document whether the Workspace or Nuotti determines each
purpose. Datatilsynet explains that the controller determines purposes and the
processor acts on its instructions; a processor relationship requires a written
data-processing agreement
([Datatilsynet: controller and processor](https://www.datatilsynet.no/rettigheter-og-plikter/virksomhetenes-plikter/behandlingsansvarlig-og-databehandler/)).
Nuotti will likely be controller for its own account, security, abuse, billing,
and service-operation purposes, even if it is processor for some
Workspace-directed participant processing. This role split requires counsel
review rather than a blanket contractual label.

The MVP needs:

- a purpose, lawful basis, data fields, recipients, role, and retention period
  recorded for every processing activity;
- just-in-time audience information at join, plus a full privacy notice meeting
  GDPR Articles 13/14;
- data minimisation: no audience email/account, and a random Session-scoped
  participant identifier rather than cross-event tracking;
- access controls, encryption, tenant isolation, incident response, processor
  agreements, and approved safeguards for transfers outside the EEA;
- self-service Workspace deletion and a request workflow for access,
  correction, erasure, restriction, portability, and objection;
- deletion propagation to caches, replicas, search indexes, and processors,
  with backup expiry documented rather than promising instantaneous physical
  erasure; and
- a separate legal-hold mechanism that records the applicable exception and
  prevents ordinary use while evidence is retained.

GDPR storage limitation requires erasure or anonymisation once data is no longer
necessary, and Datatilsynet recommends deletion deadlines or periodic review
([Datatilsynet: storage limitation](https://www.datatilsynet.no/rettigheter-og-plikter/personvernprinsippene/grunnleggende-personvernprinsipper/lagringsbegrensning/)).
Erasure rights are not absolute, but the service must make them operable
([GDPR Articles 5 and 17](https://eur-lex.europa.eu/eli/reg/2016/679/oj)).

Recommended planning defaults, to validate against actual operational needs,
are:

| Data | Product default |
|---|---|
| Current answer and reconnect token | Delete or irreversibly anonymise shortly after Session closure |
| Named leaderboard and answer history | Workspace-configurable short period; default deletion after 30 days |
| Uploaded show assets | Until Workspace/user deletion or contract termination; remove active copies promptly and expire backups on a documented cycle |
| Security and abuse logs | Fixed short period justified by security need, with restricted access |
| Takedown/licence dispute evidence | Separate case retention based on limitation periods and active claims, reviewed periodically |
| Song Catalog metadata | Retain while catalogued, subject to source/licence provenance and correction processes |

Exact periods belong in a retention schedule approved before launch. Do not use
content or participant data for analytics, model training, catalog enrichment,
or cross-Workspace recommendations unless that separate purpose, legal basis,
notice, and opt-out/rights handling have been designed.

## Shared Song Catalog

Keeping Nuotti's shared catalog to title, artist, stable identifiers, and
licensed factual metadata materially reduces content risk, but does not make
every source free to copy wholesale. In the EU/EEA, a database can receive
copyright and/or sui-generis database protection where the statutory thresholds
are met
([Directive 96/9/EC, Articles 3 and 7](https://eur-lex.europa.eu/eli/dir/1996/9/oj)).
Seed the catalog from a source whose licence expressly permits Nuotti's
commercial use, keep provenance and update/delete terms per record, and avoid
scraping a protected third-party catalog.

## Launch gates

Before public beta:

- [ ] Select initial territories; have local counsel produce a rights matrix for
      upload, cache, lyric/image display, live performance, and transmissions.
- [ ] Obtain a licensed/provenance-tracked source for Song Catalog metadata.
- [ ] Add Workspace warranties, the limited service licence, prohibited-content
      terms, cooperation duties, and termination/deletion terms.
- [ ] Ship tenant isolation, local-cache revocation/expiry, asset deletion, and
      preflight rights attestation.
- [ ] Publish and test a Norway/EU notice-and-action procedure.
- [ ] If serving the U.S., register/publish a DMCA agent and implement notice,
      counter-notice, restoration, and repeat-infringer workflows.
- [ ] Complete the GDPR role and lawful-basis matrix, processor agreements,
      privacy notices, transfer assessment, retention schedule, request handling,
      and breach process.
- [ ] Recheck the DSA's Norwegian EEA status immediately before launch.

## Decision for the Wayfinder map

Proceed with private, tenant-isolated Workspace uploads and metadata-only shared
catalog entries, but treat rights clearance, privacy, retention, deletion, and
notice handling as launch gates rather than post-MVP paperwork. The
implementation specification should contain explicit asset provenance,
attestation, deletion, quarantine, audit, and legal-hold capabilities. Public or
cross-Workspace asset sharing remains out of scope.
