# EPAM IT Marathon 2026 – Homework #8

## Automated End-to-End Testing – Green & Tasty

**Author:** Yan Kolesnykov

## Live Demo

**[Green Tasty Restaurant“ Open Website](https://dl8v07zbkza4a.cloudfront.net/main)**

**Direction:** 1 – Group Pre-Order

**Testing approach:** Page Object Model (POM) with API-assisted E2E testing

## Overview

This project contains automated tests for the Green & Tasty restaurant application developed during EPAM IT Marathon 2026.

The main goal is to verify the group pre-order functionality from the reservation owner's perspective, including participant management, order consolidation, submission, and guest removal.

The tests interact with an Angular frontend and a deployed AWS backend.

## Technology Stack

| Component | Technology |
|---|---|
| Programming language | C# |
| Framework | .NET 10 |
| Test framework | NUnit |
| Browser automation | Microsoft Playwright |
| API testing | HttpClient |
| Frontend | Angular |
| Backend | AWS Lambda and API Gateway |
| CI | GitHub Actions |
| Reporting | TRX, screenshots |

## Test Coverage

### Level 1 – Group Pre-Order Overview

Tests cover:

- Owner authentication and access to the group overview.
- Loading group sessions and participants.
- Displaying participants without dishes.
- Adding dishes to guest carts.
- Displaying individual and consolidated totals.
- Submitted and pending guest carts.
- Multiple participants with different orders.
- Unauthorized access and nonexistent groups.

### Level 2 – Group Pre-Order Submission

Tests cover:

- Submitting a consolidated order through the Angular interface.
- Merging confirmed dishes into the existing order.
- Excluding unsubmitted guest carts.
- Updating order and group reservation statuses.
- Preventing duplicate submissions.
- Rejecting submission without confirmed guest carts.
- Rejecting submission without an existing order.
- Rejecting submission with an incorrect reservation ID.

### Level 3 – Guest Removal

Tests cover:

- Removing a guest from a group through the API.
- Updating the participant list and totals.
- Reflecting guest removal in the Angular Owner View.
- Preventing deletion of the Owner session.
- Handling nonexistent guests and repeated deletion.
- Completing a full group-order workflow after removing a participant.

An additional contract test verifies that the DELETE response includes the Owner and matches the updated group state. This test is separated from the main suite until the corresponding Backend changes are deployed.

## Test Architecture

The automation project follows the Page Object Model pattern and separates browser operations from API requests.

**Project structure:**

- `aqa/GreenTasty.Aqa/Api` – HTTP API client.
- `aqa/GreenTasty.Aqa/Config` – environment configuration.
- `aqa/GreenTasty.Aqa/Fixtures` – test data setup and cleanup.
- `aqa/GreenTasty.Aqa/Pages` – Playwright page objects.
- `aqa/GreenTasty.Aqa/Tests/Level1` – group overview tests.
- `aqa/GreenTasty.Aqa/Tests/Level2` – order submission tests.
- `aqa/GreenTasty.Aqa/Tests/Level3` – guest removal tests.
- `.github/workflows/aqa.yml` – automated CI pipeline.

API calls are used to prepare temporary reservations, create guest sessions, configure carts, and verify saved Backend state. Playwright is used to validate the real Angular interface.

## Local Setup

**Prerequisites:**

- .NET SDK 10
- Node.js 22 (compatible with the Angular project's `package.json`)
- npm
- PowerShell
- Chromium installed through Playwright

### 1. Start the frontend

From the repository root:

```powershell
cd frontend/restaurant-frontend-app
npm ci
npm.cmd start
```

The frontend runs at `http://localhost:4200`.

### 2. Configure test environment

Open another PowerShell window:

```powershell
$env:AQA_FRONTEND_URL = "http://localhost:4200"
$env:AQA_API_URL = "https://ob2ahveso3.execute-api.eu-north-1.amazonaws.com/develop"
$env:AQA_OWNER_EMAIL = Read-Host "Owner email"
$env:AQA_OWNER_PASSWORD = Read-Host "Owner password"
```

Use a dedicated test account. Do not commit passwords, access tokens, or credentials to Git.

### 3. Install Playwright

```powershell
cd aqa/GreenTasty.Aqa
dotnet build
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\bin\Debug\net10.0\playwright.ps1" install chromium
```

### 4. Run the tests

All tests currently compatible with the deployed Backend:

```powershell
dotnet test --filter "TestCategory!=PostDeploy"
```

Run an individual level:

```powershell
dotnet test --filter "TestCategory=Level1"
dotnet test --filter "TestCategory=Level2"
dotnet test --filter "TestCategory=Level3&TestCategory!=PostDeploy"
```

Run one test class:

```powershell
dotnet test --filter "FullyQualifiedName~FullGroupFlowE2ETests"
```

Run the contract test after deploying the updated Backend:

```powershell
dotnet test --filter "TestCategory=PostDeploy"
```

## CI Pipeline

GitHub Actions is configured in `.github/workflows/aqa.yml`.

The workflow:

1. Checks out the repository.
2. Installs .NET and Node.js.
3. Installs Angular dependencies.
4. Installs Playwright Chromium.
5. Starts the local Angular development server.
6. Runs NUnit and Playwright tests against the configured AWS API.
7. Uploads test reports and failure artifacts.

The workflow requires two GitHub Actions secrets:

- `AQA_OWNER_EMAIL`
- `AQA_OWNER_PASSWORD`

Configure them in **Settings → Secrets and variables → Actions**.

The workflow excludes tests marked `PostDeploy` until the corresponding Backend changes are available.

## Test Reports

NUnit produces TRX test results in:

`aqa/GreenTasty.Aqa/TestResults/`

Some browser-based tests also generate screenshots on failure.

GitHub Actions uploads available reports and screenshots as workflow artifacts.

## Test Data Management

Automated tests generate temporary reservations and guest sessions using dynamically selected future dates.

Each test uses its own fixture, and reservation cleanup is performed when the fixture is disposed.

The tests run against a configured test account and environment. They should not be used with production customer data.

## Expected Behavior and Assumptions

- Only the reservation Owner may access the protected group overview and submit the consolidated order.
- Only confirmed guest carts contribute to the consolidated submission.
- Owner dishes already stored in the original reservation order are retained.
- A group order cannot be submitted twice.
- The Owner session cannot be removed.
- Removing a nonexistent or previously removed guest returns HTTP 404.
- Removing a guest requires the session to belong to the specified group.
- Guest removal is not allowed after the group is submitted or closed.
- If the Owner View does not provide a guest-removal button, removal is exercised through the API and the updated result is verified in the browser.

## Current Verification Status

**Local execution:** 20 automated tests passed, 0 failed.

**Backend contract verification:** one additional post-deployment test is available.

**CI verification:** pending the first successful GitHub Actions execution.

The previously deployed CloudFront frontend does not contain the latest Owner View. Browser tests therefore use the current Angular frontend running locally or in CI, together with the AWS API.

## Notes

This repository contains the automation solution and the frontend required to execute browser tests.

The Backend implementation is maintained separately as part of the earlier EPAM Marathon homework.

The optional ReportPortal integration and cloud-hosted execution pipeline are not included in the current implementation.
