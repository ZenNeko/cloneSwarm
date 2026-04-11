"""
Extract all Claude Code conversation logs (main + subagents) into one readable Markdown file.
Filters to user/assistant text messages only.
"""
import json, os, re
from pathlib import Path
from datetime import datetime

LOG_DIR = Path(r"C:\Users\choro\.claude\projects\F--cloneSwarm")
OUTPUT  = Path(r"F:\cloneSwarm\conversation_logs.md")

def collect_jsonl_files():
    files = list(LOG_DIR.rglob("*.jsonl"))
    files.sort(key=lambda f: f.stat().st_mtime)
    return files

def extract_text_from_content(content):
    """Extract readable text from message content."""
    if isinstance(content, str):
        return content
    if not isinstance(content, list):
        return ""

    parts = []
    for block in content:
        if isinstance(block, str):
            parts.append(block)
        elif isinstance(block, dict):
            btype = block.get("type", "")
            if btype == "text":
                parts.append(block.get("text", ""))
            elif btype == "tool_use":
                tool = block.get("name", "?")
                inp = block.get("input", {})
                if isinstance(inp, dict):
                    fp = inp.get("file_path", inp.get("path", ""))
                    cmd = inp.get("command", "")
                    pattern = inp.get("pattern", "")
                    prompt = inp.get("prompt", "")
                    if fp:
                        parts.append(f"[Tool: {tool} -> {fp}]")
                    elif cmd:
                        parts.append(f"[Tool: {tool} -> {cmd[:100]}]")
                    elif pattern:
                        parts.append(f"[Tool: {tool} -> pattern: {pattern[:80]}]")
                    elif prompt:
                        parts.append(f"[Tool: {tool} -> {prompt[:150]}]")
                    else:
                        parts.append(f"[Tool: {tool}]")
            elif btype == "thinking":
                # Skip thinking blocks for readability
                pass
            elif btype == "tool_result":
                # Skip tool results
                pass
    return "\n".join(p for p in parts if p.strip())

def extract_messages(filepath):
    messages = []
    with open(filepath, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            try:
                obj = json.loads(line)
            except json.JSONDecodeError:
                continue

            entry_type = obj.get("type", "")
            if entry_type not in ("user", "assistant"):
                continue

            msg = obj.get("message", {})
            if not msg:
                continue

            role = msg.get("role", entry_type)
            content = msg.get("content", "")
            text = extract_text_from_content(content)

            if not text.strip():
                continue

            # Clean system-reminder tags
            text = re.sub(r'<system-reminder>.*?</system-reminder>', '', text, flags=re.DOTALL)
            text = text.strip()
            if not text:
                continue

            # Truncate extremely long messages
            if len(text) > 8000:
                text = text[:8000] + f"\n\n... [truncated — {len(text)} chars total]"

            timestamp = obj.get("timestamp", "")
            ts_str = ""
            if timestamp:
                try:
                    if isinstance(timestamp, (int, float)):
                        ts_str = datetime.fromtimestamp(timestamp / 1000).strftime("%Y-%m-%d %H:%M")
                    elif isinstance(timestamp, str):
                        ts_str = timestamp[:16]
                except:
                    pass

            messages.append({
                "role": "user" if role == "user" else "assistant",
                "text": text,
                "time": ts_str
            })
    return messages

def get_session_label(filepath):
    rel = filepath.relative_to(LOG_DIR)
    parts = rel.parts
    if "subagents" in parts:
        session_id = parts[0][:12]
        agent_name = filepath.stem
        return f"Subagent: {agent_name}", session_id
    else:
        session_id = filepath.stem[:12]
        return f"Main Conversation", session_id

def main():
    files = collect_jsonl_files()
    print(f"Found {len(files)} JSONL files")

    # Group by session directory
    sessions = {}
    for f in files:
        rel = f.relative_to(LOG_DIR)
        # Session ID = first directory or filename
        if len(rel.parts) > 1:
            session_id = rel.parts[0]
        else:
            session_id = rel.stem
        if session_id not in sessions:
            sessions[session_id] = []
        sessions[session_id].append(f)

    total_msgs = 0
    with open(OUTPUT, "w", encoding="utf-8") as out:
        out.write("# Claude Code Conversation Logs — Swarm Survivors\n\n")
        out.write(f"> Extracted: {datetime.now().strftime('%Y-%m-%d %H:%M')}\n")
        out.write(f"> Sessions: {len(sessions)}\n")
        out.write(f"> Log files: {len(files)}\n\n")
        out.write("---\n\n")

        for sid, session_files in sessions.items():
            main_files = [f for f in session_files if "subagents" not in str(f)]
            sub_files  = [f for f in session_files if "subagents" in str(f)]
            sub_files.sort(key=lambda f: f.stat().st_mtime)

            out.write(f"# Session: `{sid[:16]}...`\n\n")

            # Main conversation first
            for filepath in main_files:
                msgs = extract_messages(filepath)
                if not msgs:
                    continue
                total_msgs += len(msgs)
                out.write(f"## Main Conversation ({len(msgs)} messages)\n\n")
                for m in msgs:
                    label = "USER" if m["role"] == "user" else "CLAUDE"
                    time_tag = f" *({m['time']})*" if m["time"] else ""
                    out.write(f"### {label}{time_tag}\n\n")
                    out.write(m["text"] + "\n\n")
                out.write("\n---\n\n")

            # Subagents
            if sub_files:
                out.write(f"## Subagents ({len(sub_files)} agents)\n\n")
                for filepath in sub_files:
                    msgs = extract_messages(filepath)
                    if not msgs:
                        continue
                    total_msgs += len(msgs)
                    label, _ = get_session_label(filepath)
                    out.write(f"### {label} ({len(msgs)} msgs)\n\n")
                    for m in msgs:
                        role_label = "PROMPT" if m["role"] == "user" else "AGENT"
                        out.write(f"**{role_label}:** {m['text']}\n\n")
                    out.write("---\n\n")

    size_mb = OUTPUT.stat().st_size / (1024 * 1024)
    print(f"Done! {total_msgs} messages extracted")
    print(f"Output: {OUTPUT}")
    print(f"File size: {size_mb:.1f} MB")

if __name__ == "__main__":
    main()
