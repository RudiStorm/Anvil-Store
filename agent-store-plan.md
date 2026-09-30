# Anvil Store Plan

## Purpose

Anvil Store is a production-shaped, single-tenant marketplace for free and
open-source Anvil templates and providers. The store distributes editable source
code rather than compiled or opaque packages. A future Anvil CLI will consume
the store's package metadata and immutable artifacts, but CLI integration is not
part of this project phase.

The application must demonstrate how Anvil can support a real server-rendered
product with Razor UI, Identity, EF Core, authorization, audit logging, file
uploads, durable workflows, mail, storage boundaries, partial HTML updates,
observability, and production diagnostics.

## Approved Scope

### In scope

- Public storefront for templates and providers.
- Free and open-source source downloads.
- Constrained ZIP package format.
- Single-tenant application.
- Local storage for development and the first deployment target.
- Real malware scanning through ClamAV or an equivalent configured scanner.
- Manual approval for every submitted release.
- Creator profiles and creator submissions.
- Administrator and moderator management areas.
- Authenticated download history.
- Reviews limited to authenticated users who downloaded the listing.
- Favorites.
- Immutable published releases and SHA-256 checksums.
- Dark mode with an optional light mode.
- Public listing pages and SEO metadata.
- Internal package manifest contract for future CLI/API work.

### Deferred

- Paid listings, billing, payouts, and subscriptions.
- Automated moderation approval.
- Public API endpoints for CLI consumption.
- CLI implementation and `anvil new --template` integration.
- Remote object storage.
- Multi-tenancy and organizations.
- Package signing and trust-chain distribution.
- Automated dependency vulnerability scanning.
- Full marketplace analytics and creator monetization.

## Package Model

The first release supports exactly two package types:

- `template`: an application or feature starter containing editable Razor,
  C#, CSS, configuration, migrations, tests, and documentation.
- `provider`: a reusable integration or application capability containing source,
  registration instructions, configuration, tests, and documentation.

Every upload is a constrained ZIP archive with this layout:

```text
package.zip
├── anvil-package.json
├── README.md
├── LICENSE
├── CHANGELOG.md
├── src/
├── tests/
├── docs/
├── screenshots/
└── examples/
```

Required files:

- `anvil-package.json`
- `README.md`
- `LICENSE`

Optional files are allowed only in the documented top-level directories. The
validator must reject path traversal, absolute paths, symlinks, duplicate paths,
unsafe archive entries, oversized files, oversized extracted content, unknown
top-level directories, and malformed metadata.

The manifest must contain:

- Package identifier.
- Display name and package type.
- Version.
- Supported Anvil versions.
- Supported .NET versions.
- Dependencies.
- Required Anvil profile, if applicable.
- Required database provider, if applicable.
- License identifier.
- Repository URL.
- Maintainer information.
- Installation instructions.
- Customization guidance.

Published artifacts are immutable. Each artifact stores its SHA-256 checksum,
content length, content type, scan result, scan timestamp, and storage key.

## Roles and Permissions

Roles:

- Visitor: browse public listings and package details.
- User: manage account data, download published releases, favorite listings, and
  review listings that they downloaded.
- Creator: manage own creator profile, listings, drafts, releases, and feedback.
- Moderator: review submissions, request changes, approve or reject releases,
  withdraw content, and manage abuse reports.
- Administrator: manage users, roles, categories, listings, moderation, audit,
  settings, and operational data.

Permissions:

```text
catalog.read
catalog.manage
submissions.create
submissions.read-own
submissions.review
releases.publish
users.manage
roles.manage
reviews.moderate
reports.manage
audit.read
operations.manage
```

Authorization must be applied consistently to pages, form posts, fragments,
procedures, downloads, and background operations. Creators must never approve
their own submissions.

## Public Storefront

Routes:

```text
/
/providers
/providers/{slug}
/templates
/templates/{slug}
/search
/categories/{slug}
/creators/{slug}
```

Capabilities:

- Search by name, description, identifier, tags, and creator.
- Filter by package type, category, license, compatibility, and recency.
- Public listing details with README, screenshots, changelog, versions,
  dependencies, compatibility, license, author, and customization guidance.
- Download published source archives.
- Record authenticated download history.
- Favorite and unfavorite listings.
- Submit, edit, and report reviews when eligibility requirements are met.
- Report abuse or licensing concerns.
- Generate page titles, descriptions, canonical URLs, and sitemap entries.

## Creator Portal

Routes:

```text
/creator
/creator/listings
/creator/listings/new
/creator/listings/{id}
/creator/listings/{id}/edit
/creator/listings/{id}/versions
/creator/submissions
/creator/settings
```

Creator workflow:

1. Create a draft listing.
2. Add metadata, documentation, license, compatibility, and repository details.
3. Upload a constrained ZIP archive.
4. Validate archive structure and manifest.
5. Scan the archive for malware.
6. Submit a clean release for manual review.
7. Receive approval, rejection, or requested changes.
8. Publish an approved immutable release.
9. Create later versions through the same review process.

Creators can view their own submission history and basic download counts, but
cannot change published artifacts or approve their own releases.

## Administration and Moderation

Routes:

```text
/admin
/admin/users
/admin/roles
/admin/listings
/admin/submissions
/admin/releases
/admin/categories
/admin/reports
/admin/audit
/admin/storage
/admin/jobs
/admin/settings
```

Moderation states:

```text
Draft
Uploaded
Scanning
ScanFailed
AwaitingReview
ChangesRequested
Approved
Rejected
Published
Withdrawn
```

Moderators can approve, reject, request changes, withdraw published releases,
and record review notes. Administrators can manage platform configuration,
roles, users, categories, reports, audit records, storage state, and jobs.

## Domain Model

The application owns its EF Core model and migrations. Initial entities:

- `ApplicationUser`
- `CreatorProfile`
- `StoreListing`
- `ListingVersion`
- `PackageArtifact`
- `ListingCategory`
- `ListingTag`
- `ListingDependency`
- `ListingScreenshot`
- `SubmissionReview`
- `DownloadRecord`
- `Favorite`
- `Review`
- `AbuseReport`
- `ModerationAction`

Important invariants:

- Listing identifiers and slugs are normalized and unique.
- Package versions are immutable after publication.
- Only clean, approved releases are downloadable.
- A published artifact cannot be replaced in place.
- A review requires an authenticated download of the listing.
- A user can have at most one active review per listing.
- Sensitive mutations and moderation actions are audited.
- Creator-owned drafts are private to the creator and authorized administrators.

## Upload and Malware Scanning

Local storage is outside `wwwroot` and is divided into explicit areas:

```text
storage/
├── quarantine/
├── accepted/
├── published/
└── temporary/
```

Uploads must:

- Enforce request, archive, file, and extracted-size limits.
- Stream to quarantine rather than buffering unbounded content.
- Validate archive entries before extraction.
- Reject traversal, symlinks, absolute paths, duplicates, and unsafe names.
- Validate `anvil-package.json` against the application contract.
- Calculate a SHA-256 checksum.
- Run a real malware scan before moderation.
- Never compile or execute uploaded source in the web process.
- Keep scan results and failures in the audit trail.

The scanner boundary is application-owned:

```text
IMalwareScanner
ClamAvMalwareScanner
DevelopmentMalwareScanner
```

Production configuration must fail clearly when the required scanner is not
available. Tests may use a deterministic test scanner.

## Authentication and Security

- Use ASP.NET Core Identity through Anvil's Identity integration.
- Require email confirmation in production configuration.
- Support password reset, MFA, recovery codes, lockout, and device sessions.
- Persist Data Protection keys outside the application process.
- Require antiforgery on cookie-authenticated mutations.
- Return browser redirects for page authentication and `401` for API-style
  requests.
- Do not expose storage paths or internal artifact keys.
- Use signed or authorized download responses.
- Rate-limit authentication, uploads, reviews, reports, and downloads.
- Encode README and changelog content safely; do not treat uploaded Markdown as
  trusted HTML.
- Never log passwords, tokens, raw credentials, or full private artifacts.

## Anvil Capabilities to Demonstrate

- Razor server-rendered pages and layouts.
- Typed route definitions and generated links.
- Request context and request memoization.
- Identity, roles, claims, and permission policies.
- EF Core persistence and explicit migrations.
- Audit logging and compliance boundaries.
- Multipart file handling.
- Local storage abstraction.
- HTMX and Anvil fragment updates.
- Partial form submissions with full-page fallbacks.
- Background jobs and transactional outbox.
- Mail notifications for submission decisions.
- OpenAPI metadata for internal endpoints.
- Sitemap generation.
- Health and readiness endpoints.
- OpenTelemetry and structured operations diagnostics.
- Rate limiting, compression, and caching.
- Dark/light theme preferences.

## Implementation Milestones

### Milestone 1: Application Foundation

- Create `samples/Anvil.Store`.
- Add relational SQLite persistence and application-owned `StoreDbContext`.
- Add Identity user model and initial roles/permissions.
- Add production-shaped configuration and migrations.
- Add shared public, creator, and admin layouts.
- Add dark/light theme tokens and responsive navigation.

### Milestone 2: Package and Storage Foundation

- Add package metadata entities and manifest contracts.
- Add constrained ZIP validator.
- Add local quarantine/accepted/published storage.
- Add checksum generation.
- Add malware scanner abstraction and ClamAV adapter.
- Add upload limits and safe archive handling.

### Milestone 3: Public Storefront

- Add provider and template catalog pages.
- Add listing detail pages.
- Add search, filters, categories, and creator profiles.
- Add sitemap and SEO metadata.
- Add authorized immutable downloads.

### Milestone 4: Creator Workflow

- Add creator profile management.
- Add draft listing creation and editing.
- Add package upload and validation results.
- Add release submission and status history.
- Add creator download summary.

### Milestone 5: Moderation and Administration

- Add submission review queue.
- Add approval, rejection, requested changes, and withdrawal.
- Add user, role, category, report, and listing management.
- Add audit and storage inspection pages.
- Add job and readiness status pages.

### Milestone 6: Community Features

- Add download history.
- Add favorites.
- Add eligible reviews and ratings.
- Add abuse reports and moderation.
- Add notification emails.

### Milestone 7: Verification and Documentation

- Add authorization and antiforgery tests.
- Add archive security and malware scanner tests.
- Add full-page and fragment browser tests.
- Add accessibility and response-size checks.
- Add production configuration checks.
- Document the package manifest and future CLI contract.
- Run build, test, generation, migration, and production diagnostics checks.

## Testing Requirements

Tests must cover:

- Public catalog and listing rendering.
- Creator ownership isolation.
- Admin and moderator authorization.
- Anonymous `401` and unauthorized `403` behavior.
- Antiforgery failures.
- ZIP traversal, symlink, duplicate, malformed, and oversized archive rejection.
- Valid package manifest acceptance.
- Malware-positive and scanner-unavailable behavior.
- Quarantine-to-published lifecycle.
- Immutable artifact downloads and checksums.
- Review eligibility based on recorded downloads.
- Review ownership and moderation.
- Audit records for uploads, approvals, withdrawals, and downloads.
- Dark/light theme behavior and accessibility.
- Full-page versus fragment response contracts.
- Sitemap and metadata output.
- Response-size smoke tests.

## Definition of Done

The store is ready for the first release when:

- A creator can upload an editable provider or template ZIP.
- The application rejects unsafe or invalid archives.
- A real malware scanner runs before moderation.
- A moderator can approve or reject every release manually.
- Users can download approved source code and receive its checksum.
- Users can review only listings they downloaded.
- Administrators can manage the catalog and permissions.
- All important actions are audited.
- Public and authenticated pages work in dark and light themes.
- No uploaded code is executed by the store.
- The project uses relational persistence rather than process-local catalog state.
- Relevant build, test, accessibility, security, and production checks pass.
- `build plan.md` records completed sample milestones accurately.
