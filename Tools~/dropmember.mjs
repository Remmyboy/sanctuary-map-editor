// Delete C# members whose signature line matches a regex, through their
// matching closing brace (or a single-line `;` member).
//   node Tools~/dropmember.mjs <file> <regex> [<regex>...]
// Prints what it removed. Comments are not parsed: braces inside strings or
// comments in a removed member would confuse it; check the diff.
import fs from 'node:fs';

const [file, ...patterns] = process.argv.slice(2);
const raw = fs.readFileSync(file, 'utf8');
const nl = raw.includes('\r\n') ? '\r\n' : '\n';
let lines = raw.split(/\r?\n/);

for (const pat of patterns) {
	const re = new RegExp(pat);
	let removed = 0;
	for (let i = 0; i < lines.length; i++) {
		if (!re.test(lines[i])) continue;
		// Member start: include attribute / doc-comment lines directly above.
		let start = i;
		while (start > 0 && /^\s*(\[|\/\/\/)/.test(lines[start - 1])) start--;
		let end = i, depth = 0, opened = false;
		for (let j = i; j < lines.length; j++) {
			for (const ch of lines[j]) {
				if (ch === '{') { depth++; opened = true; }
				else if (ch === '}') depth--;
			}
			if (!opened && lines[j].trimEnd().endsWith(';')) { end = j; break; }
			if (opened && depth === 0) { end = j; break; }
		}
		console.log(`${file}: removed lines ${start + 1}-${end + 1}: ${lines[i].trim()}`);
		lines.splice(start, end - start + 1);
		// collapse a doubled blank line left behind
		if (start < lines.length && start > 0 && lines[start].trim() === '' && lines[start - 1].trim() === '') lines.splice(start, 1);
		removed++;
		i = start - 1;
	}
	if (!removed) console.log(`${file}: no match for /${pat}/`);
}
fs.writeFileSync(file, lines.join(nl));
