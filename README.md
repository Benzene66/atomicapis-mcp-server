# AtomicAPIs MCP Server

A [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that exposes 17 production-grade micro-API tools for AI assistants. Built with .NET 9 and the official [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) SDK.

MCP allows AI assistants like Claude to invoke these tools natively during conversation — no HTTP requests, API keys, or boilerplate needed on the client side.

## Tools

| Tool | Method | Description |
|------|--------|-------------|
| **WebToMarkdownTools** | `ExtractMarkdown` | Scrape any URL and convert the page to clean Markdown. Strips nav, footer, scripts, and non-essential HTML to minimize tokens. SSRF-protected with a 5MB page limit. |
| **EmailShieldTools** | `VerifyEmail` | Check if an email address is disposable or from a known burner domain. Validates format, checks against a curated domain list, and verifies MX records via DNS. |
| **TimezoneTools** | `ResolveTimezone` | Convert latitude/longitude to full timezone data including IANA timezone, UTC offset, DST status, current local time, and next transition. |
| **SchemaSniffTools** | `InferSchema` | Auto-detect the format (JSON, CSV, XML) of a payload and infer its schema — field names, types, nullability, and nesting. |
| **ProductSchemaTools** | `GenerateProductSchema` | Generate JSON-LD Product structured data for SEO. Outputs both raw JSON-LD and a ready-to-embed `<script>` tag. |
| **CsvSurgeonTools** | `CleanCsv` | Clean, normalize, and deduplicate CSV data. Fixes encoding, normalizes headers, collapses whitespace, pads/truncates rows, and optionally normalizes date formats. |
| **UtmLinkTools** | `CloakLink` | Create cloaked UTM-tagged tracking URLs. Encode campaign parameters into a short token and decode them back. |
| **JsonRepairTools** | `RepairJson` | Fix broken JSON from LLM outputs — trailing commas, unquoted keys, single quotes, missing brackets. Optionally validate against a JSON schema. |
| **TokenCounterTools** | `CountTokens` | Count tokens for multiple LLM models (GPT-4, Claude, Llama, Mistral, Gemini). Estimates costs, context window usage, and flags when text exceeds the model's limit. |
| **EmailAuthGraderTools** | `GradeEmailAuth` | Audit a domain's email authentication setup: SPF, DKIM, DMARC, MTA-STS, and BIMI. Returns a letter grade (A–F), score, and actionable recommendations. |
| **PiiRedactorTools** | `RedactPii` | Detect and redact personally identifiable information from text. Handles emails, SSNs, credit cards, phone numbers, IP addresses, street addresses, dates of birth, and URLs. |
| **PhoneValidatorTools** | `ValidatePhone` | Validate international phone numbers across 30+ countries. Returns E.164 format, country info, number type (mobile/landline), and formatted output. |
| **TaxIdValidatorTools** | `ValidateTaxId` | Validate VAT and Tax IDs for EU, US, UK, Australia, and India. EU VAT numbers are verified against the VIES service when available. |
| **PromptInjectionTools** | `DetectPromptInjection` | Score text for prompt injection and jailbreak attempts. Uses pattern matching and heuristic analysis across multiple attack categories. Returns a risk score (0–100) with flagged spans. |
| **SemanticSearchTools** | `SemanticSearch` | Match a search query against a catalog of items using TF-IDF similarity. Supports fuzzy matching, synonym expansion, and configurable title weighting. |
| **RelatedContentTools** | `FindRelatedContent` | Find related content items from a corpus using TF-IDF and Jaccard similarity. Configurable thresholds, category boosting, and optional score breakdowns. |
| **VoiceTaskParserTools** | `ParseTasks` | Extract actionable tasks from voice transcripts. Identifies action verbs, assignees, priorities, deadlines, and confidence scores. Handles up to 100,000 characters. |

## Getting Started

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build

```bash
cd src
dotnet build
```

### Run

```bash
cd src
dotnet run
```

The server communicates over stdio using the MCP JSON-RPC protocol. It is designed to be launched by an MCP-compatible client (e.g., Claude Desktop, Claude Code, or any MCP host).

### Configure with Claude Desktop

Add this to your Claude Desktop configuration (`claude_desktop_config.json`):

```json
{
  "mcpServers": {
    "atomicapis": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/atomicapis-mcp-server/src"]
    }
  }
}
```

### Configure with Claude Code

```bash
claude mcp add atomicapis -- dotnet run --project /path/to/atomicapis-mcp-server/src
```

## Architecture

```
src/
├── Program.cs              # MCP server entry point (stdio transport)
├── Tools/                  # MCP tool definitions (17 tools)
│   ├── WebToMarkdownTools.cs
│   ├── EmailShieldTools.cs
│   ├── TimezoneTools.cs
│   └── ...
└── Engines/                # Core logic (pure computation, no HTTP/DI concerns)
    ├── CsvCleaner.cs
    ├── SchemaInferrer.cs
    ├── EmailValidator.cs
    └── ...
```

**Tools** define the MCP interface — parameter descriptions, validation, and response formatting. Each tool delegates to an **Engine** class that contains the pure business logic with no framework dependencies.

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| [ModelContextProtocol](https://www.nuget.org/packages/ModelContextProtocol) | 1.1.0 | MCP SDK for .NET |
| [Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting) | 9.0.6 | Host builder for DI and lifecycle |
| [HtmlAgilityPack](https://www.nuget.org/packages/HtmlAgilityPack) | 1.12.4 | HTML parsing (Web-to-Markdown) |
| [ReverseMarkdown](https://www.nuget.org/packages/ReverseMarkdown) | 5.2.0 | HTML-to-Markdown conversion |
| [GeoTimeZone](https://www.nuget.org/packages/GeoTimeZone) | 6.1.0 | Coordinate-to-timezone lookup |
| [DnsClient](https://www.nuget.org/packages/DnsClient) | 1.8.0 | DNS queries (email auth grading) |

## License

MIT
