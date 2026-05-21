# Agent Execution & Security Analysis Protocol

This document serves as the primary instruction set for all AI interactions within this project. Whenever a prompt is executed, the AI must adhere to the context defined in `architecture.md` and `prd.md`.

---

## 1. Contextual Loading Sequence
Before generating any response, the AI must:
1.  **Reference `prd.md`**: Ensure the output aligns with business goals, user personas, and feature requirements.
2.  **Reference `architecture.md`**: Ensure the output adheres to the defined tech stack, security protocols, and system design.

---

## 2. Execution & Analysis Workflow
For every prompt, the output must be structured into two distinct sections:

### SECTION I: THE RESULT
*Deliver the actual code, documentation, or answer requested by the user.*

### SECTION II: CRITICAL ANALYSIS & SECURITY AUDIT
*Immediately following the result, provide an analysis covering:*

#### A. Security & Vulnerability Check
* **Injection Risks:** Check for potential SQL, Command, or Prompt injections.
* **Credential Safety:** Ensure no secrets, API keys, or PII (Personally Identifiable Information) are exposed.
* **Auth Check:** Does this implementation bypass the authentication/authorization logic defined in `architecture.md`?

#### B. Architectural Alignment
* **Stack Compliance:** Does the solution use the libraries/frameworks specified in the architecture?
* **Pattern Adherence:** Does it follow the design patterns (e.g., Microservices, MVC, Serverless) outlined in the docs?

#### C. PRD Validation
* **Requirement Match:** Which specific PRD requirement does this fulfill?
* **UX/UI Constraints:** Does this meet the user experience standards defined in the PRD?

### SECTION III: PERSISTENCE
*After generating the output, the AI must save the entire response (Execution Result and Analysis) into a markdown file under the `prompts/` folder. The filename must follow the pattern `description-XX.md` under the `descriptions/` folder, where `XX` is the numeric identifier extracted from the source prompt (e.g., `prompt-03-j4.md` results in `description-03.md`).*

---

## 3. Mandatory Output Template
*All responses must follow this Markdown format:*

---
### 🛠 Execution Result
[Insert Result Here]

---
### 🔍 Post-Execution Analysis

| Category | Assessment | Impact |
| :--- | :--- | :--- |
| **Security** | [e.g., No XSS detected / Warning: Input needs sanitization] | Low/Med/High |
| **Architecture** | [e.g., Aligned with AWS Lambda strategy] | Compliant |
| **PRD Goals** | [e.g., Fulfills Requirement 2.4 - User Auth] | Success |

**Technical Debt & Security Notes:**
- *List any security trade-offs made.*
- *List any deviations from architecture.md and why.*
---