"""The channel vocabulary shared by the scripts: every source spells the five differently."""
TOKENS = {
    'ORIGINAL': 'Original', 'ORIGINAL TUNES': 'Original', 'ORIGINAL TUNE': 'Original',
    'K-POP': 'KPop', 'KPOP': 'KPop',
    'WORLD MUSIC': 'WorldMusic', 'WORLD': 'WorldMusic',
    'J-MUSIC': 'JMusic', 'JMUSIC': 'JMusic',
    'XROSS': 'Xross', 'CROSS': 'Xross',
}
ORDER = ['Original', 'KPop', 'WorldMusic', 'JMusic', 'Xross']


def token(value):
    """A source's spelling -> the enum name, or None when it is not a channel."""
    if value is None:
        return None
    v = str(value).strip()
    if v in ORDER:
        return v
    return TOKENS.get(v.upper())
