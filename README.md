# Trailhead Switchback

> This file is the authoritative specification for Trailhead Switchback.
> AI tools must treat it as normative input, not background documentation.

Trailhead Switchback is a deliberately scoped demo application that shows how AI can be used to assist with real-world decision making **without giving up determinism, safety, or clarity**.

The project automatically routes new incoming email from Gmail and Microsoft 365 into folders or labels using a set of **explicit, ordered rules**, with a large language model used only to interpret intent and select the appropriate rule.

This is not an autonomous agent.  
It does not learn, guess, or optimize over time.  
Every decision is explainable by configuration and testable by design.

---

## What This Project Demonstrates

- Using an LLM as a **judgment engine**, not a controller
- Deterministic behavior enforced through contracts and unit tests
- Real-time, event-driven processing with Gmail and Microsoft 365
- Practical Azure architecture using Azure Functions
- Infrastructure-as-code with Terraform
- Clear failure boundaries and safe defaults
- How different AI coding tools perform when given the *same constraints*

---

## What This Project Intentionally Does *Not* Do

To keep the demo focused and honest, Switchback intentionally avoids:

- Learning from user behavior
- Confidence scoring or ranking outputs
- Processing historical inbox data
- Automatic replies or drafting
- Multi-user or team inbox scenarios
- "Agent" or autonomous behavior

The goal is clarity, not cleverness.

---

## High-Level Architecture

1. A new email arrives in Gmail or Microsoft 365  
2. The provider sends a push notification  
3. An Azure Function fetches the message  
4. Email content and ordered rules are sent to an LLM in a single call  
5. The model returns one matching rule or `NONE`  
6. The email is labeled (Gmail) or moved to a folder (M365)  
7. A small activity record is written for visibility  

If anything fails, the email is left untouched.

---

## Core Design Principles

- **Deterministic outcomes**  
  At most one rule applies per email. If the system is unsure, it does nothing.

- **AI as interpretation, not control**  
  The LLM selects a rule. The system enforces behavior.

- **Safe by default**  
  Failures never move or modify email.

- **Test-first contracts**  
  Anything deterministic is unit tested.

---

## Why the Name "Switchback"

A switchback is a deliberate change in direction used to make difficult terrain manageable.

This project applies the same idea to automation:
- clear paths
- controlled decisions
- intentional constraints

AI is used where it adds leverage, and nowhere else.

---

## Admin Capabilities (MVP)

- Connect / disconnect Gmail and Microsoft 365 accounts
- Create, edit, reorder, enable, and disable rules
- View a simple recent activity list (last 50 items)

That's it. No test mode, no dry runs, no simulators.

---

## Rule Evaluation Model

- Rules are shared across Gmail and Microsoft 365
- Rules are evaluated in priority order
- The LLM is given:
  - sender name + email
  - subject
  - first N characters of the body (globally configured)
  - the full ordered rule list
- The LLM must return **exactly one rule name** or `NONE`

Anything else is treated as a failure.

---

## Testing Philosophy

Switchback uses **TUnit** for unit testing.

Unit tests define correctness for:
- LLM output parsing
- Prompt construction and truncation
- Provider action mapping (label vs move)
- Idempotency and deduplication
- Failure handling

External systems (LLMs, Gmail, Microsoft Graph) are mocked.  
Azure plumbing is intentionally thin and not unit tested.

---

## Infrastructure

- Azure Functions (C#, isolated worker)
- Azure Table Storage (or Cosmos Table API)
- Azure Key Vault (envelope encryption for OAuth tokens)
- Application Insights (minimal)
- Managed Identity
- Terraform for all Azure resources

All infrastructure is defined as code and supports multiple environments.

---

## Repository Structure & Branches

This repository is structured to make **AI-assisted development behavior observable and comparable**.

### `main`
**Requirements only.**

- Product requirements, scope constraints, and acceptance criteria
- MCP prompts used with AI coding tools
- Architectural decisions and guardrails
- No application code

This branch is the **single source of truth** for what is being built.

---

### `copilot-cli`
Implementation generated primarily using **GitHub Copilot CLI** with MCP access.

---

### `cursor`
Implementation generated primarily using **Cursor**.

---

### `claude-code`
Implementation generated primarily using **Claude Code**.

---

## Final Thought

Switchback is intentionally small.

That's the point.

It's easier to talk honestly about AI when the system around it is simple enough to understand.
