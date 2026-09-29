"""Cross-check the EN translation template against the EKLang strings in the
source. A key in the code but not in the template is invisible to translators;
a key in the template but not in the code is dead weight (usually a message
whose English text was edited, which silently untranslates it)."""
import re
import glob
import io
import os

os.chdir(os.path.dirname(os.path.abspath(__file__)))

PAT = re.compile(r'EKLang\.[TF]\(\s*"((?:[^"\\]|\\.)*)"')


def unescape(s):
    return s.replace('\\n', '\n').replace('\\"', '"')


keys = set()
for path in glob.glob('src/*.cs'):
    text = io.open(path, encoding='utf-8').read()
    for m in PAT.finditer(text):
        keys.add(unescape(m.group(1)))

template = set()
for line in io.open('package/LangMod/EN/elonakeys.tsv', encoding='utf-8'):
    line = line.rstrip('\n')
    if not line.strip() or line.startswith('#'):
        continue
    template.add(unescape(line.split('\t')[0]))

missing = sorted(k for k in keys if k not in template)
stale = sorted(k for k in template if k not in keys)

print('code strings: %d   template keys: %d' % (len(keys), len(template)))
print('\nMISSING from template (%d):' % len(missing))
for k in missing:
    print('  ' + k.replace('\n', '\\n'))
print('\nSTALE in template (%d):' % len(stale))
for k in stale:
    print('  ' + k.replace('\n', '\\n'))
