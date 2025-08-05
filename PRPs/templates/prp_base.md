# PRP: [Feature Name]

## Project Context
**Why**: [Core business/user need being addressed]
**What**: [Specific deliverable and success criteria]  
**Where**: [Integration points, affected systems/files]

## Codebase Context
**Architecture**: [Relevant patterns, conventions, and constraints from CLAUDE.md]
**Dependencies**: [Libraries, frameworks, and their versions]
**Patterns**: [Existing similar features to reference - include file paths]
**Tests**: [Testing approach and existing test patterns to follow]

## Research Findings
**Documentation**: [URLs to official docs, with specific sections]
**Examples**: [Code snippets, GitHub repos, Stack Overflow solutions]
**Gotchas**: [Library quirks, version issues, common pitfalls]
**Best Practices**: [Industry standards and patterns to follow]

## Implementation Plan

### Pseudocode Approach
```
[High-level pseudocode showing the implementation flow]
```

### File Structure
- `file1.py`: [Purpose and key functions]
- `file2.py`: [Purpose and key functions]  
- `tests/test_feature.py`: [Test scenarios to cover]

### Integration Points
- [List where this feature connects to existing code]
- [Database changes, API modifications, UI updates]

### Task Sequence
1. [First task - most foundational]
2. [Second task - builds on first]
3. [Continue in logical dependency order]

## Error Handling Strategy
**Common Failures**: [Anticipated failure modes]
**Validation**: [How to verify each step works]
**Rollback**: [How to undo if something breaks]

## Validation Gates

### Syntax & Style
```bash
# [Commands to check code quality]
ruff check --fix && mypy .
```

### Unit Tests
```bash  
# [Commands to run tests]
uv run pytest tests/ -v
```

### Integration Tests
```bash
# [Commands for end-to-end validation]
```

## Anti-Patterns to Avoid
- [Specific things NOT to do based on research]
- [Common mistakes in this domain]
- [Project-specific constraints from CLAUDE.md]

## Quality Checklist
- [ ] All requirements from INITIAL.md addressed
- [ ] Follows existing code patterns and conventions
- [ ] Includes comprehensive error handling
- [ ] Has unit tests with good coverage
- [ ] Documentation updated where needed
- [ ] Validation commands all pass
- [ ] Integration points working correctly

## Implementation Confidence
**Score**: [1-10] - Confidence in one-pass implementation success
**Reasoning**: [Why this score - what might cause issues]

---
*Context is King: Include ALL necessary documentation, examples, and caveats*