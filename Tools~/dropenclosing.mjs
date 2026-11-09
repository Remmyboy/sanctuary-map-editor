// Remove the member (method, constructor, property, field) enclosing each
// given line of a C# file - for deleting code that the compiler says uses
// something removed.
//   node Tools~/dropenclosing.mjs <file> <line> [<line>...]
// A member is found by walking up to the nearest line that starts a member
// at a shallower brace depth than the error line, then to its matching
// closing brace. Lines are processed bottom-up so earlier numbers stay valid.
import fs from 'node:fs';

const [file, ...nums] = process.argv.slice(2);
const raw = fs.readFileSync(file, 'utf8');
const nl = raw.includes('\r\n') ? '\r\n' : '\n';
let lines = raw.split(/\r?\n/);

// Brace depth at the start of each line.
function depths() {
	const d = [];
	let depth = 0;
	for (const l of lines) {
		d.push(depth);
		for (const ch of l.replace(/\/\/.*$/, '').replace(/"(?:[^"\\]|\\.)*"/g, '""').replace(/'(?:[^'\\]|\\.)'/g, "''")) {
			if (ch === '{') depth++;
			else if (ch === '}') depth--;
		}
	}
	return d;
}

const memberStart = /^\s*(\[.*\]\s*)?((public|private|protected|internal|static|override|virtual|readonly|async|new|abstract|sealed|partial)\s+)*([\w<>\[\],.?]+\s+)+[\w<>]+\s*(\(|=|;|\{|$)/;
const notMember = /^\s*(if|else|for|foreach|while|switch|case|return|using|try|catch|finally|do|lock|var|yield|throw|break|continue)\b/;

const targets = [...new Set(nums.map(Number))].sort((a, b) => b - a);
let lastStart = Infinity;
for (const n of targets) {
	if (n - 1 >= lastStart) continue; // already inside a removed member
	const d = depths();
	const target = n - 1;
	let s = -1;
	for (let i = target; i >= 0; i--) {
		if (d[i] < d[target] || i === target) {
			if (memberStart.test(lines[i]) && !notMember.test(lines[i]) && d[i] <= d[target]) {
				// must be a member at class level: the line before it ends a member or opens the class
				s = i;
				// keep walking out while this start is itself inside a method body
				const insideMethod = (() => {
					for (let k = i - 1; k >= 0; k--) {
						if (d[k] < d[i]) return /\)\s*$/.test(lines[k].trim()) || /\)\s*$/.test((lines[k - 1] || '').trim()) && lines[k].trim() === '{';
					}
					return false;
				})();
				if (!insideMethod) break;
			}
		}
	}
	if (s < 0) { console.log(`${file}:${n}: no enclosing member found`); continue; }
	while (s > 0 && /^\s*(\[|\/\/\/)/.test(lines[s - 1])) s--;
	// end: matching brace, or the `;` of a field
	let e = s, depth = 0, opened = false;
	for (let j = s; j < lines.length; j++) {
		for (const ch of lines[j]) { if (ch === '{') { depth++; opened = true; } else if (ch === '}') depth--; }
		if (!opened && lines[j].trimEnd().endsWith(';')) { e = j; break; }
		if (opened && depth === 0) { e = j; break; }
	}
	if (target > e) { console.log(`${file}:${n}: enclosing member ends before the line (${s + 1}-${e + 1}); skipped`); continue; }
	console.log(`${file}: removed ${s + 1}-${e + 1}: ${lines[s].trim().slice(0, 90)}`);
	lines.splice(s, e - s + 1);
	lastStart = s;
}
fs.writeFileSync(file, lines.join(nl));
