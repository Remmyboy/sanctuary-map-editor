// Fast compile check without Unity: builds the Unity-generated
// Assembly-CSharp.csproj with dotnet, after dropping <Compile> entries for
// files that no longer exist and adding new .cs files under Assets/Scripts.
// Regenerate the .csproj with Unity first (Rider package):
//   Unity.exe -batchmode -quit -projectPath . -executeMethod Packages.Rider.Editor.RiderScriptEditor.SyncSolution
//   node Tools~/fastcheck.mjs
import fs from 'node:fs';
import path from 'node:path';
import { execSync } from 'node:child_process';

const root = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..');
const proj = path.join(root, 'Assembly-CSharp.csproj');
let xml = fs.readFileSync(proj, 'utf8');

const listed = new Set();
xml = xml.replace(/\s*<Compile Include="([^"]+)" \/>/g, (m, rel) => {
	const file = path.join(root, rel);
	if (!fs.existsSync(file)) return '';
	listed.add(path.normalize(rel).toLowerCase());
	return m;
});

const added = [];
(function walk(dir) {
	for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
		const p = path.join(dir, e.name);
		if (e.isDirectory()) { if (e.name !== 'Editor') walk(p); continue; }
		if (!e.name.endsWith('.cs')) continue;
		const rel = path.relative(root, p);
		if (!listed.has(path.normalize(rel).toLowerCase())) added.push(rel);
	}
})(path.join(root, 'Assets', 'Scripts'));
if (added.length) xml = xml.replace('</ItemGroup>', added.map(r => `    <Compile Include="${r}" />`).join('\n') + '\n  </ItemGroup>');

fs.writeFileSync(proj, xml);
let out = '';
try { out = execSync(`dotnet build "${proj}" -nologo -v q -clp:NoSummary`, { encoding: 'utf8', cwd: root }); }
catch (e) { out = (e.stdout || '') + (e.stderr || ''); }
const errors = [...new Set(out.split(/\r?\n/).filter(l => / error /.test(l)).map(l => l.replace(root + path.sep, '').replace(/\s*\[.*\]$/, '')))];
if (process.argv.includes('--group')) {
	// One block per file: line and message, at most 8 per file.
	const by = new Map();
	for (const e of errors) {
		const m = e.match(/^(.*?)\((\d+),\d+\): error CS\d+: (.*)$/);
		if (!m) continue;
		const f = m[1].replace(/^Assets[\\/]Scripts[\\/]/, '');
		if (!by.has(f)) by.set(f, []);
		by.get(f).push(`${m[2]}: ${m[3].replace(/\s*\(are you missing.*$/, '').slice(0, 100)}`);
	}
	for (const [f, list] of by) {
		console.log(`${f} (${list.length})`);
		for (const l of [...new Set(list)].slice(0, 8)) console.log('   ' + l);
	}
} else for (const e of errors.slice(0, 80)) console.log(e);
console.log(`${errors.length} error(s)${added.length ? `, ${added.length} new file(s) added to the project` : ''}`);
process.exit(errors.length ? 1 : 0);
