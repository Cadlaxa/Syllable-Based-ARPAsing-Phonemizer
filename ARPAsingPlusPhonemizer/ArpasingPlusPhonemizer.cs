using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenUtau.Api;
using OpenUtau.Core.G2p;
using Serilog;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Arpasing+ Phonemizer", "EN ARPA+", "Cadlaxa", language: "EN")]
    // Custom ARPAsing Phonemizer for OU
    // main focus of this Phonemizer is to bring fallbacks to existing available alias from
    // all ARPAsing banks
    public class ArpasingPlusPhonemizer : SyllableBasedPhonemizer {
        protected IG2p g2p;
        private readonly string[] vowels = {
        "aa", "ax", "ae", "ah", "ao", "aw", "ay", "eh", "er", "ey", "ih", "iy", "ow", "oy", "uh", "uw", "a", "e", "i", "o", "u", "ai", "ei", "oi", "au", "ou", "ix", "ux",
        "aar", "ar", "axr", "aer", "ahr", "aor", "or", "awr", "aur", "ayr", "air", "ehr", "eyr", "eir", "ihr", "iyr", "ir", "owr", "our", "oyr", "oir", "uhr", "uwr", "ur",
        "aal", "al", "axl", "ael", "ahl", "aol", "ol", "awl", "aul", "ayl", "ail", "ehl", "el", "eyl", "eil", "ihl", "iyl", "il", "owl", "oul", "oyl", "oil", "uhl", "uwl", "ul",
        "naan", "an", "axn", "aen", "ahn", "aon", "on", "awn", "aun", "ayn", "ain", "ehn", "en", "eyn", "ein", "ihn", "iyn", "in", "own", "oun", "oyn", "oin", "uhn", "uwn", "un",
        "aang", "ang", "axng", "aeng", "ahng", "aong", "ong", "awng", "aung", "ayng", "aing", "ehng", "eng", "eyng", "eing", "ihng", "iyng", "ing", "owng", "oung", "oyng", "oing", "uhng", "uwng", "ung",
        "aam", "am", "axm", "aem", "ahm", "aom", "om", "awm", "aum", "aym", "aim", "ehm", "em", "eym", "eim", "ihm", "iym", "im", "owm", "oum", "oym", "oim", "uhm", "uwm", "um",
        };
        private readonly string[] consonants = "b,ch,d,dh,dr,dx,f,g,hh,jh,k,l,m,n,ng,p,q,r,s,sh,t,th,tr,v,w,y,z,zh".Split(',');
        private readonly string[] affricates = "ch,jh,j".Split(',');
        private readonly string[] tapConsonant = "dx".Split(",");
        private readonly string[] semilongConsonants = "y,w,ng,n,m,v,z,q,hh".Split(",");
        private readonly string[] connectingGlides = "l,r".Split(",");
        private readonly string[] longConsonants = "ch,f,jh,s,sh,th,zh,dr,tr,ts,j".Split(",");
        private readonly string[] normalConsonants = "b,d,dh,g,k,p,t,l,r".Split(',');
        private readonly string[] connectingNormCons = "b,d,g,k,p,t,dh".Split(',');
        private readonly Dictionary<string, string> dictionaryReplacements = ("aa=aa;ae=ae;ah=ah;ao=ao;aw=aw;ax=ax;ay=ay;" +
            "b=b;ch=ch;d=d;dh=dh;" + "dx=dx;eh=eh;er=er;ey=ey;f=f;g=g;hh=hh;ih=ih;iy=iy;jh=jh;k=k;l=l;m=m;n=n;ng=ng;ow=ow;oy=oy;" +
            "p=p;q=q;r=r;s=s;sh=sh;t=t;th=th;" + "uh=uh;uw=uw;v=v;w=w;" + "y=y;z=z;zh=zh").Split(';')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        protected override string[] GetVowels() => vowels;
        protected override string[] GetConsonants() => consonants;
        protected override string GetDictionaryName() => "cmudict-0_7b.txt";
        protected override Dictionary<string, string> GetDictionaryPhonemesReplacement() => dictionaryReplacements;

        // For banks with missing vowels
        private readonly Dictionary<string, string> missingVphonemes = "ax=ah,aa=ah,ae=ah,iy=ih,uh=uw,ix=ih,ux=uw".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private readonly Dictionary<string, string> missingCphonemes = "by=b,dy=d,fy=f,gy=g,hy=hh,jy=jh,ky=k,ly=l,my=m,py=p,ry=r,sy=s,ty=t,vy=v,zy=z,bw=b,chw=ch,dw=d,fw=f,gw=g,hw=hh,jw=jh,kw=k,lw=l,mw=m,nw=n,pw=p,rw=r,sw=s,tw=t,vw=v,zw=w,ts=t,nx=n,cl=q,wh=w,dx=d,zh=sh,dh=d".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;

        // TIMIT symbols
        private readonly Dictionary<string, string> timitphonemes = "axh=ax,bcl=b,dcl=d,eng=ng,gcl=g,hv=hh,kcl=k,pcl=p,tcl=t".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isTimitPhonemes = false;

        private readonly Dictionary<string, string> vvExceptions =
            new Dictionary<string, string>() {
                {"aw","w"},
                {"ow","w"},
                {"uw","w"},
                {"uh","w"},
                {"ay","y"},
                {"ey","y"},
                {"iy","y"},
                {"oy","y"},
                {"ih","y"},
                {"er","r"},
                {"aar","r"},
                {"aen","n"},
                {"aeng","ng"},
                {"aor","r"},
                {"ehr","r"},
                {"ihng","ng"},
                {"ihr","r"},
                {"uwr","r"},
                {"awn","n"},
                {"awng","ng"},
                {"el","l"},
            };

        protected override string[] GetSymbols(Note note) {
            string[] original = base.GetSymbols(note);
            if (original == null) {
                return null;
            }
            List<string> modified = new List<string>();
            // SPLITS UP DR AND TR
            string[] tr = new[] { "tr" };
            string[] dr = new[] { "dr" };
            string[] av_c = new[] { "al", "am", "an", "ang", "ar" };
            string[] ev_c = new[] { "el", "em", "en", "eng" };
            string[] iv_c = new[] { "il", "im", "in", "ing", "ir" };
            string[] ov_c = new[] { "ol", "om", "on", "ong", "or" };
            string[] uv_c = new[] { "ul", "um", "un", "ung", "ur" };
            var consonatsV1 = new List<string> { "l", "m", "n", "r" };
            var consonatsV2 = new List<string> { "mm", "nn", "ng" };
            // SPLITS UP 2 SYMBOL VOWELS AND 1 SYMBOL CONSONANT
            List<string> vowel3S = new List<string>();
            foreach (string V1 in vowels) {
                foreach (string C1 in consonatsV1) {
                    vowel3S.Add($"{V1}{C1}");
                }
            }
            // SPLITS UP 2 SYMBOL VOWELS AND 2 SYMBOL CONSONANT
            List<string> vowel4S = new List<string>();
            foreach (string V1 in vowels) {
                foreach (string C1 in consonatsV2) {
                    vowel3S.Add($"{V1}{C1}");
                }
            }
            foreach (string s in original) {
                if (dr.Contains(s) && !HasOto($"{s} {vowels}", note.tone) && !HasOto($"ay {s}", note.tone)) {
                    modified.AddRange(new string[] { "jh", s[1].ToString() });
                } else if (tr.Contains(s) && !HasOto($"{s} {vowels}", note.tone) && !HasOto($"ay {s}", note.tone)) {
                    modified.AddRange(new string[] { "ch", s[1].ToString() });
                } else if (av_c.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { "aa", s[1].ToString() });
                } else if (ev_c.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { "eh", s[1].ToString() });
                } else if (iv_c.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { "iy", s[1].ToString() });
                } else if (ov_c.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { "ao", s[1].ToString() });
                } else if (uv_c.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { "uw", s[1].ToString() });
                } else if (vowel3S.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { s.Substring(0, 2), s[2].ToString() });
                } else if (vowel4S.Contains(s) && !HasOto($"b {s}", note.tone) && !HasOto(ValidateAlias(s), note.tone)) {
                    modified.AddRange(new string[] { s.Substring(0, 2), s.Substring(2, 2) });
                } else {
                    modified.Add(s);
                }
            }
            return modified.ToArray();

        }

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();

            // LOAD DICTIONARY FROM FOLDER
            string path = Path.Combine(PluginDir, "arpasing.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, Data.Resources.arpasing_template);
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());

            // LOAD DICTIONARY FROM SINGER FOLDER
            if (singer != null && singer.Found && singer.Loaded) {
                string file = Path.Combine(singer.Location, "arpasing.yaml");
                if (File.Exists(file)) {
                    try {
                        g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(file)).Build());
                    } catch (Exception e) {
                        Log.Error(e, $"Failed to load {file}");
                    }
                }
            }
            g2ps.Add(new ArpabetG2p());
            return new G2pFallbacks(g2ps.ToArray());
        }

        protected override List<string> ProcessSyllable(Syllable syllable) {
            string prevV = syllable.prevV;
            string[] cc = syllable.cc;
            string v = syllable.v;
            string basePhoneme;
            var phonemes = new List<string>();
            var symbols = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;
            symbols.Add(syllable.prevV == "" ? "-" : syllable.prevV);
            symbols.AddRange(syllable.cc);
            for (int i = 0; i < symbols.Count - 1; i++) {
                phonemes.Add($"{symbols[i]} {symbols[i + 1]}");
            }


            if (!HasOto("ax", syllable.tone)) {
                isMissingVPhonemes = true;
            }
            if (!HasOto("bw", syllable.tone)) {
                isMissingCPhonemes = true;
            }
            if (!HasOto("gcl", syllable.tone)) {
                isTimitPhonemes = true;
            }

            // STARTING V
            if (syllable.IsStartingV) {
                // TRIES - V, -V, THEN V
                var rv = $"- {v}";
                if (HasOto(rv, syllable.vowelTone) || HasOto(ValidateAlias(rv), syllable.vowelTone)) {
                    basePhoneme = rv;
                } else {
                    basePhoneme = v;
                }
            }
            // V V
            else if (syllable.IsVV) {
                if (!CanMakeAliasExtension(syllable)) {
                    basePhoneme = $"{prevV} {v}";
                    if (!HasOto(basePhoneme, syllable.vowelTone) && vvExceptions.ContainsKey(prevV) && prevV != v) {
                        // VV IS NOT PRESENT, CHECKS VVEXCEPTIONS LOGIC
                        var vc = $"{prevV} {vvExceptions[prevV]}";
                        if (!HasOto(vc, syllable.vowelTone)) {
                            vc = $"{prevV} {vvExceptions[prevV]}";
                        }
                        phonemes.Add(vc);
                        var crv = $"{vvExceptions[prevV]} {v}";
                        var cv = $"{vvExceptions[prevV]}{v}";
                        basePhoneme = cv;
                        if (!HasOto(cv, syllable.vowelTone)) {
                            basePhoneme = crv;
                        }
                    } else {
                        {
                            var diphthongVowel = new List<string> { "aw", "ay", "ey", "iy", "ow", "oy", "uw" };
                            var diphthongVV = new List<string> { };
                            var nonDiphthongVowels = vowels.Except(diphthongVowel);
                            var vv = $"{prevV} {v}";
                            // GENERATES DIPHTHONG VV COMBINATIONS
                            foreach (var vowel1 in diphthongVowel) {
                                foreach (var vowel2 in diphthongVowel) {
                                    diphthongVV.Add($"{vowel1} {vowel2}");
                                }
                            }
                            // CHECK IF VV CONTAINS DIPHTHONGS
                            bool basePhonemeContainsDiphthongs = diphthongVV.Any(d => basePhoneme.Contains(d));
                            // CHECK IF VV CONTAINS WITHOUT DIPHTHONGS
                            bool hasOtoContainsVvWithoutDiphthongs = !diphthongVV.Any(d => HasOto(d, syllable.vowelTone));
                            // LOGIC OF VV BASEPHONEME
                            if (!HasOto(basePhoneme, syllable.vowelTone)) {
                                if (basePhonemeContainsDiphthongs) {
                                    basePhoneme = vv;
                                } else if (hasOtoContainsVvWithoutDiphthongs) {
                                    basePhoneme = vv;
                                } else {
                                    basePhoneme = v;
                                }
                            }

                        }

                    }
                } else {
                    // PREVIOUS ALIAS WILL EXTEND
                    basePhoneme = null;
                }
            }
            // IF VCV (EXPERIMENTAL)
            else { // VCV
                var vcv = $"{prevV} {cc[0]}{v}";
                var vccv = $"{prevV} {string.Join("", cc)}{v}";
                var crv = $"{cc.Last()} {v}";
                if (syllable.IsVCVWithOneConsonant && (HasOto(vcv, syllable.vowelTone) || HasOto(ValidateAlias(vcv), syllable.vowelTone))) {
                    basePhoneme = vcv;
                } else if (syllable.IsVCVWithMoreThanOneConsonant && (HasOto(vccv, syllable.vowelTone) || HasOto(ValidateAlias(vccv), syllable.vowelTone))) {
                    basePhoneme = vccv;
                } else {
                    basePhoneme = cc.Last() + v;
                    if (!HasOto(cc.Last() + v, syllable.vowelTone) && (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone))) {
                        basePhoneme = crv;
                    }
                }
            }
            // C V OR CV
            if (syllable.IsStartingCVWithOneConsonant) {
                var crv = $"{cc[0]} {v}";
                var cv = $"{cc[0]}{v}";
                if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                    basePhoneme = crv;
                } else if (!HasOto(crv, syllable.vowelTone) && HasOto(cv, syllable.vowelTone)) {
                    basePhoneme = cv;
                } else if (!HasOto(crv, syllable.vowelTone) && !HasOto(cv, syllable.vowelTone)) {
                    basePhoneme = v;
                }
            }
            phonemes.Add(basePhoneme);
            return phonemes;
        }

        protected override List<string> ProcessEnding(Ending ending) {
            var phonemes = new List<string>();
            var symbols = new List<string>();
            symbols.Add(ending.prevV);
            symbols.AddRange(ending.cc);
            symbols.Add("-");

            for (int i = 0; i < symbols.Count - 1; i++) {
                phonemes.Add($"{symbols[i]} {symbols[i + 1]}");
            }
            string[] cc = ending.cc;
            string v = ending.prevV;

            return phonemes;
        }

        protected override string ValidateAlias(string alias) {
            //FALLBACKS
            //CV (IF CV HAS NO C AND V FALLBACK)
            if (alias == "ng ae") {
                return alias.Replace("ng ae", "n ae");
            } else if (alias == "ch ao") {
                return alias.Replace("ch ao", "sh ow");
            } else if (alias == "dh ao") {
                return alias.Replace("ao", "ow");
            } else if (alias == "dh oy") {
                return alias.Replace("oy", "ow");
            } else if (alias == "jh ao") {
                return alias.Replace("ao", "oy");
            } else if (alias == "ao -") {
                return alias.Replace("ao -", "aa -");
            } else if (alias == "v ao") {
                return alias.Replace("v ao", "b ao");
            } else if (alias == "z ao") {
                return alias.Replace("z ao", "s ao");
            } else if (alias == "ng eh") {
                return alias.Replace("ng eh", "n eh");
            } else if (alias == "z eh") {
                return alias.Replace("z eh", "s eh");
            } else if (alias == "jh er") {
                return alias.Replace("jh er", "z er");
            } else if (alias == "ng er") {
                return alias.Replace("ng er", "n er");
            } else if (alias == "r er") {
                return alias.Replace("r er", "er");
            } else if (alias == "th er") {
                return alias.Replace("th er", "th r");
            } else if (alias == "jh ey") {
                return alias.Replace("ey", "ae");
            } else if (alias == "ng ey") {
                return alias.Replace("ng ey", "n ey");
            } else if (alias == "th ey") {
                return alias.Replace("ey", "ae");
            } else if (alias == "zh ey") {
                return alias.Replace("zh ey", "jh ae");
            } else if (alias == "ch ow") {
                return alias.Replace("ch ow", "sh ow");
            } else if (alias == "jh ow") {
                return alias.Replace("ow", "oy");
            } else if (alias == "v ow") {
                return alias.Replace("v ow", "b ow");
            } else if (alias == "th ow") {
                return alias.Replace("th ow", "s ow");
            } else if (alias == "z ow") {
                return alias.Replace("z ow", "s ow");
            } else if (alias == "ch oy") {
                return alias.Replace("ch oy", "sh ow");
            } else if (alias == "th oy") {
                return alias.Replace("th oy", "s ao");
            } else if (alias == "v oy") {
                return alias.Replace("v oy", "b oy");
            } else if (alias == "w oy") {
                return alias.Replace("oy", "ao");
            } else if (alias == "z oy") {
                return alias.Replace("oy", "aa");
            } else if (alias == "ch uh") {
                return alias.Replace("ch uh", "sh uh");
            } else if (alias == "dh uh") {
                return alias.Replace("dh uh", "d uw");
            } else if (alias == "jh uh") {
                return alias.Replace("uh", "uw");
            } else if (alias == "ng uh") {
                return alias.Replace("ng uh", "n uw");
            } else if (alias == "th uh") {
                return alias.Replace("th uh", "f uw");
            } else if (alias == "v uh") {
                return alias.Replace("v uh", "b uh");
            } else if (alias == "z uh") {
                return alias.Replace("z uh", "s uw");
            } else if (alias == "ch uw") {
                return alias.Replace("ch uw", "sh uw");
            } else if (alias == "dh uw") {
                return alias.Replace("dh uw", "d uw");
            } else if (alias == "g uw") {
                return alias.Replace("g uw", "k uw");
            } else if (alias == "jh uw") {
                return alias.Replace("jh uw", "sh uw");
            } else if (alias == "ng uw") {
                return alias.Replace("ng uw", "n uw");
            } else if (alias == "th uw") {
                return alias.Replace("th uw", "f uw");
            } else if (alias == "v uw") {
                return alias.Replace("v uw", "b uw");
            } else if (alias == "z uw") {
                return alias.Replace("z uw", "s uw");
            } else if (alias == "zh aa") {
                return alias.Replace("zh aa", "sh ah");
            } else if (alias == "zh ae") {
                return alias.Replace("zh ae", "sh ah");
            } else if (alias == "ng oy") {
                return alias.Replace("oy", "ow");
            }

            // VALIDATE ALIAS DEPENDING ON METHOD
            if (isMissingVPhonemes || isMissingCPhonemes || isTimitPhonemes) {
                foreach (var syllable in missingVphonemes.Concat(missingCphonemes).Concat(timitphonemes)
                    ) {
                    alias = alias.Replace(syllable.Key, syllable.Value);
                }
            }

            var replacements = new Dictionary<string, string[]> {
                { "ao", new[] { "ow" } },
                { "oy", new[] { "ow" } },
                { "aw", new[] { "ah" } },
                { "ay", new[] { "ah" } },
                { "eh", new[] { "ae" } },
                { "ey", new[] { "eh" } },
                { "ow", new[] { "ao" } }
            };
            foreach (var kvp in replacements) {
                var v1 = kvp.Key;
                var vfallbacks = kvp.Value;
                foreach (var vfallback in vfallbacks) {
                    foreach (var c1 in consonants) {
                        alias = alias.Replace(c1 + " " + v1, c1 + " " + vfallback);
                    }
                }
            }

            Dictionary<string, List<string>> vvReplacements = new Dictionary<string, List<string>>
            {
            //VV (diphthongs)
            //ay
            { "ay aw", new List<string> { "y ae" } },
            { "ay ax", new List<string> { "y ah" } },
            { "ay ay", new List<string> { "y ah" } },
            { "ay oy", new List<string> { "y ow" } },
            //ey
            { "ey aw", new List<string> { "y ae" } },
            { "ey ax", new List<string> { "y ah" } },
            { "ey ay", new List<string> { "y ah" } },
            { "ey ey", new List<string> { "iy ey" } },
            { "ey oy", new List<string> { "y ow" } },
            //iy
            { "iy aw", new List<string> { "y ae" } },
            { "iy ax", new List<string> { "y ah" } },
            { "iy ay", new List<string> { "y ah" } },
            { "iy ey", new List<string> { "y ey" } },
            { "iy oy", new List<string> { "y ow" } },
            //oy
            { "oy aw", new List<string> { "y ae" } },
            { "oy ax", new List<string> { "y ah" } },
            { "oy ay", new List<string> { "y ah" } },
            { "oy oy", new List<string> { "y ow" } },
            //er
            { "er aw", new List<string> { "r ae" } },
            { "er ax", new List<string> { "r ah" } },
            { "er ay", new List<string> { "r ah" } },
            { "er er", new List<string> { "r r" } },
            { "er oy", new List<string> { "r ow" } },
            { "er uh", new List<string> { "r uw" } },
            //aw
            { "aw ae", new List<string> { "w ah" } },
            { "aw aw", new List<string> { "w ae" } },
            { "aw ax", new List<string> { "w ah" } },
            { "aw ay", new List<string> { "w ah" } },
            { "aw ow", new List<string> { "w ao" } },
            { "aw oy", new List<string> { "w ao" } },
            //ow
            { "ow ae", new List<string> { "w ah" } },
            { "ow ao", new List<string> { "w ao" } },
            { "ow aw", new List<string> { "w ae" } },
            { "ow ax", new List<string> { "w ah" } },
            { "ow ay", new List<string> { "w ah" } },
            { "ow ow", new List<string> { "w ao" } },
            { "ow oy", new List<string> { "w ao" } },
            //uw
            { "uw ae", new List<string> { "w ah" } },
            { "uw aw", new List<string> { "w ae" } },
            { "uw ax", new List<string> { "w ah" } },
            { "uw ay", new List<string> { "w ah" } },
            { "uw ow", new List<string> { "w ao" } },
            { "uw oy", new List<string> { "w ao" } },
            { "uw uw", new List<string> { "w uw" } },
            };

            foreach (var kvp in vvReplacements) {
                var originalValue = kvp.Key;
                var replacementOptions = kvp.Value;

                foreach (var replacement in replacementOptions) {
                    alias = alias.Replace(originalValue, replacement);
                }
            }

            //VC (diphthongs)
            //VC (aw specific)
            if (alias == "aw ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "aw dr") {
                return alias.Replace("dr", "d");
            }
            if (alias == "aw dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "aw jh") {
                return alias.Replace("jh", "d");
            }
            if (alias == "aw ng") {
                return alias.Replace("aw ng", "uh ng");
            }
            if (alias == "aw q") {
                return alias.Replace("q", "t");
            }
            if (alias == "aw tr") {
                return alias.Replace("tr", "t");
            }
            if (alias == "aw zh") {
                return alias.Replace("zh", "d");
            }
            if (alias == "aw w") {
                return alias.Replace("aw", "ah");
            }

            //VC (ay specific)
            if (alias == "ay dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "ay dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "ay ng") {
                return alias.Replace("ay", "ih");
            }
            if (alias == "ay q") {
                return alias.Replace("q", "t");
            }
            if (alias == "ay zh") {
                return alias.Replace("zh", "jh");
            }
            if (alias == "ay tr") {
                return alias.Replace("tr", "t");
            }
            //VC (ey specific)
            if (alias == "ey dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "ey dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "ey ng") {
                return alias.Replace("ey", "ih");
            }
            if (alias == "ey q") {
                return alias.Replace("q", "t");
            }
            if (alias == "ey zh") {
                return alias.Replace("zh", "jh");
            }
            if (alias == "ey tr") {
                return alias.Replace("tr", "t");
            }
            //VC (ow specific)
            if (alias == "ow ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "ow dr") {
                return alias.Replace("dr", "d");
            }
            if (alias == "ow dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "ow jh") {
                return alias.Replace("jh", "d");
            }
            if (alias == "ow ng") {
                return alias.Replace("ow", "uh");
            }
            if (alias == "ow q") {
                return alias.Replace("q", "t");
            }
            if (alias == "ow zh") {
                return alias.Replace("zh", "z");
            }
            if (alias == "ow tr") {
                return alias.Replace("tr", "t");
            }
            //VC (oy specific)
            if (alias == "oy dx") {
                return alias.Replace("oy dx", "iy d");
            }
            if (alias == "oy dr") {
                return alias.Replace("oy dr", "iy jh");
            }
            if (alias == "oy f") {
                return alias.Replace("oy", "ih");
            }
            if (alias == "oy ng") {
                return alias.Replace("iy", "ih");
            }
            if (alias == "oy q") {
                return alias.Replace("q", "t");
            }
            if (alias == "oy tr") {
                return alias.Replace("oy tr", "iy ch");
            }
            if (alias == "oy zh") {
                return alias.Replace("oy zh", "iy jh");

            }

            // VC (aa)
            //VC (aa specific)
            foreach (var VC in new[] { "aa b" }) {
                alias = alias.Replace(VC, "aa d");
            }
            foreach (var VC in new[] { "aa dr" }) {
                alias = alias.Replace(VC, "aa d");
            }
            foreach (var VC in new[] { "aa dx" }) {
                alias = alias.Replace(VC, "aa d");
            }
            foreach (var VC in new[] { "aa q" }) {
                alias = alias.Replace(VC, "aa t");
            }
            foreach (var VC in new[] { "aa tr" }) {
                alias = alias.Replace(VC, "aa t");
            }
            foreach (var VC in new[] { "aa y" }) {
                alias = alias.Replace(VC, "ah iy");
            }
            foreach (var VC in new[] { "aa zh" }) {
                alias = alias.Replace(VC, "aa z");
            }

            //VC (ae specific)
            foreach (var VC in new[] { "ae b" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ae dr" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ae dx" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ae q" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ae tr" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ae y" }) {
                alias = alias.Replace(VC, "ah iy");
            }
            foreach (var VC in new[] { "ae zh" }) {
                alias = alias.Replace(VC, "ah z");
            }
            //VC (ah specific)
            foreach (var VC in new[] { "ah b" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ah dr" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ah dx" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ah q" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ah tr" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ah y" }) {
                alias = alias.Replace(VC, "ah iy");
            }
            foreach (var VC in new[] { "ah zh" }) {
                alias = alias.Replace(VC, "ah z");
            }

            //VC (ao)
            //VC (ao specific)
            foreach (var VC in new[] { "ao b" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ao dr" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ao dx" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ao q" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ao tr" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ao y" }) {
                alias = alias.Replace(VC, "ow y");
            }
            foreach (var VC in new[] { "ao zh" }) {
                alias = alias.Replace(VC, "ah z");
            }

            //VC (ax)
            //VC (ae specific)
            foreach (var VC in new[] { "ax b" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ax dr" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ax dx" }) {
                alias = alias.Replace(VC, "ah d");
            }
            foreach (var VC in new[] { "ax q" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ax tr" }) {
                alias = alias.Replace(VC, "ah t");
            }
            foreach (var VC in new[] { "ax y" }) {
                alias = alias.Replace(VC, "ah iy");
            }
            foreach (var VC in new[] { "ax zh" }) {
                alias = alias.Replace(VC, "ah z");
            }

            //VC (eh)
            //VC (eh specific)
            foreach (var VC in new[] { "eh b" }) {
                alias = alias.Replace(VC, "eh d");
            }
            foreach (var VC in new[] { "eh ch" }) {
                alias = alias.Replace(VC, "eh t");
            }
            foreach (var VC in new[] { "eh dr" }) {
                alias = alias.Replace(VC, "eh d");
            }
            foreach (var VC in new[] { "eh dx" }) {
                alias = alias.Replace(VC, "eh d");
            }
            foreach (var VC in new[] { "eh ng" }) {
                alias = alias.Replace(VC, "eh n");
            }
            foreach (var VC in new[] { "eh q" }) {
                alias = alias.Replace(VC, "eh t");
            }
            foreach (var VC in new[] { "eh y" }) {
                alias = alias.Replace(VC, "ae y");
            }
            foreach (var VC in new[] { "eh tr" }) {
                alias = alias.Replace(VC, "eh t");
            }
            foreach (var VC in new[] { "eh zh" }) {
                alias = alias.Replace(VC, "eh s");
            }
            //VC (er specific)
            foreach (var VC in new[] { "er ch" }) {
                alias = alias.Replace(VC, "er t");
            }
            foreach (var VC in new[] { "er dr" }) {
                alias = alias.Replace(VC, "er d");
            }
            foreach (var VC in new[] { "er dx" }) {
                alias = alias.Replace(VC, "er d");
            }
            foreach (var VC in new[] { "er jh" }) {
                alias = alias.Replace(VC, "er d");
            }
            foreach (var VC in new[] { "er ng" }) {
                alias = alias.Replace(VC, "er n");
            }
            foreach (var VC in new[] { "er q" }) {
                alias = alias.Replace(VC, "er t");
            }
            foreach (var VC in new[] { "er r" }) {
                alias = alias.Replace(VC, "er");
            }
            foreach (var VC in new[] { "er sh" }) {
                alias = alias.Replace(VC, "er s");
            }
            foreach (var VC in new[] { "eh tr" }) {
                alias = alias.Replace(VC, "eh t");
            }
            foreach (var VC in new[] { "er zh" }) {
                alias = alias.Replace(VC, "er z");
            }

            //VC (ih specific)
            foreach (var VC in new[] { "ih b" }) {
                alias = alias.Replace(VC, "ih d");
            }
            foreach (var VC in new[] { "ih dr" }) {
                alias = alias.Replace(VC, "ih d");
            }
            foreach (var VC in new[] { "ih dx" }) {
                alias = alias.Replace(VC, "ih d");
            }
            foreach (var VC in new[] { "ih hh" }) {
                alias = alias.Replace(VC, "iy hh");
            }
            foreach (var VC in new[] { "ih q" }) {
                alias = alias.Replace(VC, "ih t");
            }
            foreach (var VC in new[] { "ih tr" }) {
                alias = alias.Replace(VC, "ih t");
            }
            foreach (var VC in new[] { "ih w" }) {
                alias = alias.Replace(VC, "iy w");
            }
            foreach (var VC in new[] { "ih y" }) {
                alias = alias.Replace(VC, "iy y");
            }
            foreach (var VC in new[] { "ih zh" }) {
                alias = alias.Replace(VC, "ih z");
            }

            //VC (iy specific)
            foreach (var VC in new[] { "iy dr" }) {
                alias = alias.Replace(VC, "iy d");
            }
            foreach (var VC in new[] { "iy dx" }) {
                alias = alias.Replace(VC, "iy d");
            }
            foreach (var VC in new[] { "iy f" }) {
                alias = alias.Replace(VC, "iy hh");
            }
            foreach (var VC in new[] { "iy n" }) {
                alias = alias.Replace(VC, "iy m");
            }
            foreach (var VC in new[] { "iy ng" }) {
                alias = alias.Replace(VC, "ih ng");
            }
            foreach (var VC in new[] { "iy q" }) {
                alias = alias.Replace(VC, "iy t");
            }
            foreach (var VC in new[] { "iy tr" }) {
                alias = alias.Replace(VC, "iy t");
            }
            foreach (var VC in new[] { "iy zh" }) {
                alias = alias.Replace(VC, "iy z");
            }

            //VC (uh)
            //VC (uh specific)
            foreach (var VC in new[] { "uh ch" }) {
                alias = alias.Replace(VC, "uh t");
            }
            foreach (var VC in new[] { "uh dr" }) {
                alias = alias.Replace(VC, "uh d");
            }
            foreach (var VC in new[] { "uh dx" }) {
                alias = alias.Replace(VC, "uh d");
            }
            foreach (var VC in new[] { "uh jh" }) {
                alias = alias.Replace(VC, "uw d");
            }
            foreach (var VC in new[] { "uh q" }) {
                alias = alias.Replace(VC, "uh t");
            }
            foreach (var VC in new[] { "uh tr" }) {
                alias = alias.Replace(VC, "uh t");
            }
            foreach (var VC in new[] { "uh zh" }) {
                alias = alias.Replace(VC, "uw z");
            }

            //VC (uw specific)
            foreach (var VC in new[] { "uw ch" }) {
                alias = alias.Replace(VC, "uw t");
            }
            foreach (var VC in new[] { "uw dr" }) {
                alias = alias.Replace(VC, "uw d");
            }
            foreach (var VC in new[] { "uw dx" }) {
                alias = alias.Replace(VC, "uw d");
            }
            foreach (var VC in new[] { "uw jh" }) {
                alias = alias.Replace(VC, "uw d");
            }
            foreach (var VC in new[] { "uw ng" }) {
                alias = alias.Replace(VC, "uw n");
            }
            foreach (var VC in new[] { "uw q" }) {
                alias = alias.Replace(VC, "uw t");
            }
            foreach (var VC in new[] { "uw tr" }) {
                alias = alias.Replace(VC, "uw t");
            }
            foreach (var VC in new[] { "uw zh" }) {
                alias = alias.Replace(VC, "uw z");
            }

            //CC (b)
            //CC (b specific)
            if (alias == "b ch") {
                return alias.Replace("b ch", "t ch");
            }
            if (alias == "b dh") {
                return alias.Replace("b ch", "p dh");
            }
            if (alias == "b dr") {
                return alias.Replace("b dr", "d jh");
            }
            if (alias == "b dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "b ng") {
                return alias.Replace("b ng", "ng");
            }
            if (alias == "b v") {
                return alias.Replace("b v", "b -");
            }
            if (alias == "b tr") {
                return alias.Replace("b tr", "d t");
            }
            if (alias == "b th") {
                return alias.Replace("b th", "t th");
            }
            if (alias == "b zh") {
                return alias.Replace("zh", "z");
            }


            //CC (ch specific)
            if (alias == "ch f") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch dh") {
                return alias.Replace("dh", "-");
            }
            if (alias == "ch hh") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch l") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch m") {
                return alias.Replace("ch m", "ch -");
            }
            if (alias == "ch n") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch ng") {
                return alias.Replace("ch ng", "s n");
            }
            if (alias == "ch r") {
                return alias.Replace("ch r", "ch er");
            }
            if (alias == "ch s") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch sh") {
                return alias.Replace("ch sh", "s s");
            }
            if (alias == "ch w") {
                return alias.Replace("ch w", "ch ah");
            }
            if (alias == "ch y") {
                return alias.Replace("ch y", "ch iy");
            }
            if (alias == "ch z") {
                return alias.Replace("ch z", "s s");
            }
            if (alias == "ch -") {
                return alias.Replace("ch", "jh");
            }
            if (alias == "- ch") {
                return alias.Replace("ch", "jh");
            }

            //CC (d specific)
            if (alias == "d ch") {
                return alias.Replace("d", "t");
            }
            if (alias == "d dh") {
                return alias.Replace("d dh", "- dh");
            }
            if (alias == "d dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "d dx") {
                return alias.Replace("dx", "r");
            }
            if (alias == "d ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "d v") {
                return alias.Replace("d v", "d -");
            }
            if (alias == "d th") {
                return alias.Replace("d th", "t th");
            }
            if (alias == "d tr") {
                return alias.Replace("d tr", "t ch");
            }
            if (alias == "d zh") {
                return alias.Replace("zh", "z");
            }

            //CC (dh)
            //CC (dh specific)
            if (alias == "dh ch") {
                return alias.Replace("dh ch", "t ch");
            }
            if (alias == "dh dh") {
                return alias.Replace("dh dh", "dh d");
            }
            if (alias == "dh dr") {
                return alias.Replace("dh dr", "d jh");
            }
            if (alias == "dh dx") {
                return alias.Replace("dh dx", "dh -");
            }
            if (alias == "dh ng") {
                return alias.Replace("dh ng", "d n");
            }
            if (alias == "dh v") {
                return alias.Replace("dh v", "dh -");
            }
            if (alias == "dh tr") {
                return alias.Replace("dh tr", "d t");
            }
            if (alias == "dh zh") {
                return alias.Replace("zh", "z");
            }
            if (alias == "- dh") {
                return alias.Replace("dh", "d");
            }
            if (alias == "dh -") {
                return alias.Replace("dh", "d");
            }

            //CC (dx)
            //CC (dx specific)
            if (alias == "dx ch") {
                return alias.Replace("dx ch", "t ch");
            }
            if (alias == "dx dr") {
                return alias.Replace("dx dr", "d jh");
            }
            if (alias == "dx dx") {
                return alias.Replace("dx dx", "d -");
            }
            if (alias == "dx ng") {
                return alias.Replace("dx ng", "d n");
            }
            if (alias == "dx v") {
                return alias.Replace("dx v", "d -");
            }
            if (alias == "dx tr") {
                return alias.Replace("dx tr", "d t");
            }
            if (alias == "dx zh") {
                return alias.Replace("dx zh", "d z");
            }

            //CC (f)
            //CC (f specific)
            if (alias == "f dr") {
                return alias.Replace("f dr", "s jh");
            }
            if (alias == "f dx") {
                return alias.Replace("f dx", "f -");
            }
            if (alias == "f ng") {
                return alias.Replace("f ng", "f -");
            }
            if (alias == "f q") {
                return alias.Replace("f q", "f -");
            }
            if (alias == "f sh") {
                return alias.Replace("sh", "s");
            }
            if (alias == "f th") {
                return alias.Replace("f th", "th");
            }
            if (alias == "f tr") {
                return alias.Replace("f tr", "f -");
            }
            if (alias == "f v") {
                return alias.Replace("f v", "f -");
            }
            if (alias == "f w") {
                return alias.Replace("f w", "f uw");
            }
            if (alias == "f z") {
                return alias.Replace("z", "s");
            }
            if (alias == "f zh") {
                return alias.Replace("zh", "s");
            }
            if (alias == "f -") {
                return alias.Replace("f", "th");
            }

            //CC (g)
            //CC (g specific)
            if (alias == "g ch") {
                return alias.Replace("g ch", "t ch");
            }
            if (alias == "g dh") {
                return alias.Replace("g", "d");
            }
            if (alias == "g dr") {
                return alias.Replace("g dr", "d jh");
            }
            if (alias == "g dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "g ng") {
                return alias.Replace("g ng", "ng");
            }
            if (alias == "g v") {
                return alias.Replace("g v", "g -");
            }
            if (alias == "g tr") {
                return alias.Replace("g tr", "d t");
            }
            if (alias == "g zh") {
                return alias.Replace("zh", "z");
            }
            if (alias == "- b") {
                return alias.Replace("g", "d");
            }
            if (alias == "g -") {
                return alias.Replace("g", "d");
            }

            //CC (hh)
            //CC (hh specific)
            if (alias == "hh d") {
                return alias.Replace("hh d", "th -");
            }
            if (alias == "hh dr") {
                return alias.Replace("hh dr", "th -");
            }
            if (alias == "hh dx") {
                return alias.Replace("hh dx", "s d");
            }
            if (alias == "hh f") {
                return alias.Replace("hh", "f");
            }
            if (alias == "hh l") {
                return alias.Replace("hh", "f");
            }
            if (alias == "hh ng") {
                return alias.Replace("hh ng", "s n");
            }
            if (alias == "hh q") {
                return alias.Replace("hh p", "th -");
            }
            if (alias == "hh r") {
                return alias.Replace("hh", "f");
            }
            if (alias == "hh s") {
                return alias.Replace("hh", "f");
            }
            if (alias == "hh sh") {
                return alias.Replace("hh sh", "s s");
            }
            if (alias == "hh t") {
                return alias.Replace("hh t", "f");
            }
            if (alias == "hh th") {
                return alias.Replace("hh th", "hh");
            }
            if (alias == "hh tr") {
                return alias.Replace("hh tr", "th -");
            }
            if (alias == "hh v") {
                return alias.Replace("hh v", "th -");
            }
            if (alias == "hh w") {
                return alias.Replace("hh w", "hh uw");
            }
            if (alias == "hh y") {
                return alias.Replace("hh", "f");
            }
            if (alias == "hh z") {
                return alias.Replace("hh z", "s s");
            }
            if (alias == "hh zh") {
                return alias.Replace("hh zh", "th -");
            }
            if (alias == "hh -") {
                return alias.Replace("hh -", null);
            }

            //CC (jh specific)
            if (alias == "jh hh") {
                return alias.Replace("jh", "s");
            }
            if (alias == "jh hh") {
                return alias.Replace("hh", "-");
            }
            if (alias == "jh l") {
                return alias.Replace("jh", "f");
            }
            if (alias == "jh m") {
                return alias.Replace("jh", "s");
            }
            if (alias == "jh n") {
                return alias.Replace("jh", "s");
            }
            if (alias == "jh ng") {
                return alias.Replace("jh ng", "s n");
            }
            if (alias == "jh r") {
                return alias.Replace("jh r", "jh ah");
            }
            if (alias == "jh s") {
                return alias.Replace("jh", "f");
            }
            if (alias == "jh t") {
                return alias.Replace("jh t", "jh -");
            }
            if (alias == "jh w") {
                return alias.Replace("jh w", "jh ah");
            }
            if (alias == "jh y") {
                return alias.Replace("y", "iy");
            }
            if (alias == "jh z") {
                return alias.Replace("jh z", "s s");
            }

            //CC (k)
            //CC (k specific)
            if (alias == "k dr") {
                return alias.Replace("k dr", "k -");
            }
            if (alias == "k dx") {
                return alias.Replace("k dx", "t d");
            }
            if (alias == "k v") {
                return alias.Replace("k v", "k -");
            }
            if (alias == "k tr") {
                return alias.Replace("k tr", "k -");
            }
            if (alias == "k z") {
                return alias.Replace("z", "s");
            }
            if (alias == "k zh") {
                return alias.Replace("zh", "s");
            }

            //CC (l specific)
            if (alias == "l ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "l b") {
                return alias.Replace("l", "d");
            }
            if (alias == "l dr") {
                return alias.Replace("l dr", "- jh");
            }
            if (alias == "l dx") {
                return alias.Replace("l dx", "l d");
            }
            if (alias == "l hh") {
                return alias.Replace("l", "r");
            }
            if (alias == "l jh") {
                return alias.Replace("jh", "d");
            }
            if (alias == "l ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "l sh") {
                return alias.Replace("sh", "s");
            }
            if (alias == "l th") {
                return alias.Replace("l th", "l s");
            }
            if (alias == "l tr") {
                return alias.Replace("l tr", "- ch");
            }
            if (alias == "l zh") {
                return alias.Replace("zh", "z");
            }

            //CC (m specific)
            if (alias == "m ch") {
                return alias.Replace("m", "n");
            }
            if (alias == "m dr") {
                return alias.Replace("m dr", "- jh");
            }
            if (alias == "m dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "m hh") {
                return alias.Replace("m hh", "hh");
            }
            if (alias == "m jh") {
                return alias.Replace("jh", "d");
            }
            if (alias == "m ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "m n") {
                return alias.Replace("m n", "n");
            }
            if (alias == "m m") {
                return alias.Replace("m m", "n");
            }
            if (alias == "m q") {
                return alias.Replace("m q", "m -");
            }
            if (alias == "m r") {
                return alias.Replace("m", "n");
            }
            if (alias == "m s") {
                return alias.Replace("m", "n");
            }
            if (alias == "m sh") {
                return alias.Replace("m", "n");
            }
            if (alias == "m v") {
                return alias.Replace("m v", "m m");
            }
            if (alias == "m tr") {
                return alias.Replace("m tr", "- ch");
            }
            if (alias == "m zh") {
                return alias.Replace("zh", "z");
            }

            //CC (n specific)
            if (alias == "n dr") {
                return alias.Replace("n dr", "- jh");
            }
            if (alias == "n dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "n ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "n n") {
                return alias.Replace("n n", "n");
            }
            if (alias == "n m") {
                return alias.Replace("n m", "n");
            }
            if (alias == "n q") {
                return alias.Replace("n q", "n -");
            }
            if (alias == "n v") {
                return alias.Replace("n v", "n m");
            }
            if (alias == "n tr") {
                return alias.Replace("n tr", "- ch");
            }
            if (alias == "n zh") {
                return alias.Replace("zh", "z");
            }

            //CC (ng)
            if (alias == $"ng b" || alias == "ng d" || alias == "ng dh" || alias == "ng dr" || alias == "ng dx"
                || alias == "ng f" || alias == "ng g" || alias == "ng hh" || alias == "ng jh" || alias == "ng k"
                || alias == "ng l" || alias == "ng m" || alias == "ng n" || alias == "ng p"
                || alias == "ng r" || alias == "ng s" || alias == "ng sh" || alias == "ng t" || alias == "ng th" || alias == "ng tr"
                 || alias == "ng w" || alias == "ng y" || alias == "ng z") {
                return alias.Replace("ng", "n");
            }
            //CC (ng specific)
            if (alias == "ng ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "ng dr") {
                return alias.Replace("ng dr", "- jh");
            }
            if (alias == "ng ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "ng q") {
                return alias.Replace("ng q", "ng -");
            }
            if (alias == "ng v") {
                return alias.Replace("ng v", "ng s");
            }
            if (alias == "ng tr") {
                return alias.Replace("ng tr", "- ch");
            }
            if (alias == "ng zh") {
                return alias.Replace("zh", "z");
            }

            //CC (p)
            //CC (p specific)
            if (alias == "p dr") {
                return alias.Replace("t dr", "p -");
            }
            if (alias == "p dx") {
                return alias.Replace("p dx", "t d");
            }
            if (alias == "p v") {
                return alias.Replace("p v", "p -");
            }
            if (alias == "p tr") {
                return alias.Replace("t tr", "p -");
            }
            if (alias == "p z") {
                return alias.Replace("z", "s");
            }
            if (alias == "p zh") {
                return alias.Replace("zh", "s");
            }
            //CC (q)
            foreach (var c1 in new[] { "q" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "-" + " " + c2);
                }
            }

            //CC (r specific)
            if (alias == "r ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "r dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "r dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "r ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "r q") {
                return alias.Replace("r q", "r -");
            }
            if (alias == "r sh") {
                return alias.Replace("sh", "s");
            }
            if (alias == "r tr") {
                return alias.Replace("r tr", "r -");
            }
            if (alias == "r zh") {
                return alias.Replace("zh", "z");
            }

            //CC (s specific)
            if (alias == "s dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "s ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "s dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "s ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "s q") {
                return alias.Replace("s q", "s -");
            }
            if (alias == "s sh") {
                return alias.Replace("sh", "s");
            }
            if (alias == "s th") {
                return alias.Replace("s", "z");
            }
            if (alias == "s v") {
                return alias.Replace("s", "z");
            }
            if (alias == "s tr") {
                return alias.Replace("s tr", "s -");
            }
            if (alias == "s zh") {
                return alias.Replace("zh", "s");
            }

            //CC (sh specific)
            if (alias == "sh f") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh f") {
                return alias.Replace("f", "-");
            }
            if (alias == "sh hh") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh l") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh m") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh n") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh ng") {
                return alias.Replace("sh ng", "s n");
            }
            if (alias == "sh r") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh s") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh sh") {
                return alias.Replace("sh sh", "s s");
            }
            if (alias == "sh th") {
                return alias.Replace("sh th", "th");
            }
            if (alias == "sh w") {
                return alias.Replace("sh w", "sh uw");
            }
            if (alias == "sh y") {
                return alias.Replace("sh y", "sh iy");
            }
            if (alias == "sh z") {
                return alias.Replace("sh z", "s s");
            }
            //CC (t specific)
            if (alias == "t y") {
                return alias.Replace("y", "iy");
            }
            if (alias == "t z") {
                return alias.Replace("t", "g");
            }
            if (alias == "t zh") {
                return alias.Replace("t zh", "g z");
            }

            //CC (th)
            //CC (th specific)
            if (alias == "th dr") {
                return alias.Replace("th dr", "s jh");
            }
            if (alias == "th dx") {
                return alias.Replace("th dx", "th -");
            }
            if (alias == "th q") {
                return alias.Replace("th q", "th -");
            }
            if (alias == "th v") {
                return alias.Replace("th v", "th");
            }
            if (alias == "th y") {
                return alias.Replace("th y", "th ih");
            }
            if (alias == "th zh") {
                return alias.Replace("zh", "s");
            }

            //CC (v specific)
            if (alias == "v dh") {
                return alias.Replace("dh", "d");
            }
            if (alias == "v f") {
                return alias.Replace("v", "s");
            }
            if (alias == "v hh") {
                return alias.Replace("v", "s");
            }
            if (alias == "v l") {
                return alias.Replace("v", "s");
            }
            if (alias == "v m") {
                return alias.Replace("v", "s");
            }
            if (alias == "v n") {
                return alias.Replace("v", "s");
            }
            if (alias == "v ng") {
                return alias.Replace("v ng", "s n");
            }
            if (alias == "v r") {
                return alias.Replace("v", "s");
            }
            if (alias == "v th") {
                return alias.Replace("v th", "th");
            }
            if (alias == "v s") {
                return alias.Replace("v", "s");
            }
            if (alias == "v sh") {
                return alias.Replace("v sh", "s s");
            }
            if (alias == "v w") {
                return alias.Replace("v", "s");
            }
            if (alias == "v y") {
                return alias.Replace("v", "s");
            }
            if (alias == "v z") {
                return alias.Replace("v z", "s s");
            }

            //CC (w specific)
            if (alias == "w b") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w d") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w ch") {
                return alias.Replace("w ch", "uw t");
            }
            if (alias == "w dh") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w dr") {
                return alias.Replace("w dr", "uw d");
            }
            if (alias == "w dx") {
                return alias.Replace("w dx", "uw d");
            }
            if (alias == "w f") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w g") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w hh") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w jh") {
                return alias.Replace("w jh", "uw d");
            }
            if (alias == "w k") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w l") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w m") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w n") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w ng") {
                return alias.Replace("w ng", "uw n");
            }
            if (alias == "w p") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w q") {
                return alias.Replace("w q", "uw t");
            }
            if (alias == "w r") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w s") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w sh") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w t") {
                return alias.Replace("w ", "uw");
            }
            if (alias == "w th") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w tr") {
                return alias.Replace("w tr", "uw t");
            }
            if (alias == "w v") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w w") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w y") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w z") {
                return alias.Replace("w", "uw");
            }
            if (alias == "w zh") {
                return alias.Replace("w zh", "uw z");
            }
            if (alias == "w -") {
                return alias.Replace("w", "uw");
            }
            //CC (y specific)
            if (alias == "y b") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y d") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y ch") {
                return alias.Replace("y ch", "iy");
            }
            if (alias == "y dh") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y dr") {
                return alias.Replace("y dr", "iy d");
            }
            if (alias == "y dx") {
                return alias.Replace("y dx", "iy d");
            }
            if (alias == "y f") {
                return alias.Replace("y f", "iy hh");
            }
            if (alias == "y g") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y hh") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y jh") {
                return alias.Replace("y jh", "iy d");
            }
            if (alias == "y k") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y l") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y m") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y n") {
                return alias.Replace("y n", "iy m");
            }
            if (alias == "y ng") {
                return alias.Replace("y ng", "iy m");
            }
            if (alias == "y p") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y q") {
                return alias.Replace("y q", "iy t");
            }
            if (alias == "y r") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y s") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y sh") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y t") {
                return alias.Replace("y ", "iy");
            }
            if (alias == "y th") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y tr") {
                return alias.Replace("y tr", "iy t");
            }
            if (alias == "y v") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y w") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y y") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y z") {
                return alias.Replace("y", "iy");
            }
            if (alias == "y zh") {
                return alias.Replace("y zh", "iy z");
            }
            if (alias == "y -") {
                return alias.Replace("y", "iy");
            }

            //CC (z specific)
            if (alias == "z ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "z dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "z dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "z tr") {
                return alias.Replace("tr", "t");
            }
            if (alias == "z ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "z q") {
                return alias.Replace("z q", "z -");
            }
            if (alias == "z z") {
                return alias.Replace("z z", "z s");
            }
            if (alias == "z zh") {
                return alias.Replace("z zh", "z s");
            }
            //CC (zh)
            //CC (zh specific)
            if (alias == "zh ch") {
                return alias.Replace("ch", "t");
            }
            if (alias == "zh dr") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "zh dx") {
                return alias.Replace("dx", "d");
            }
            if (alias == "zh tr") {
                return alias.Replace("tr", "t");
            }
            if (alias == "zh ng") {
                return alias.Replace("ng", "n");
            }
            if (alias == "zh q") {
                return alias.Replace("zh q", "jh -");
            }
            if (alias == "zh z") {
                return alias.Replace("zh z", "z s");
            }
            if (alias == "zh zh") {
                return alias.Replace("z zh", "z s");
            }
            //VC's
            foreach (var v1 in new[] { "aw", "ow", "uh", }) {
                foreach (var c1 in consonants) {
                    alias = alias.Replace(v1 + " " + c1, "uw" + " " + c1);
                }
            }
            foreach (var v1 in new[] { "ay", "ey", "oy", }) {
                foreach (var c1 in consonants) {
                    alias = alias.Replace(v1 + " " + c1, "iy" + " " + c1);
                }
            }
            foreach (var v1 in new[] { "aa", "ae", "ao", "ax", "eh", "er" }) {
                foreach (var c1 in consonants) {
                    alias = alias.Replace(v1 + " " + c1, "ah" + " " + c1);
                }
            }
            // glottal
            foreach (var c1 in new[] { "q" }) {
                foreach (var v1 in vowels) {
                    alias = alias.Replace(c1 + " " + v1, "-" + " " + v1);
                }
            }
            foreach (var c1 in new[] { "q" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c2 + " " + c1, $"{c2} -");
                }
            }
            foreach (var c1 in new[] { "q" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, $"- {c2}");
                }
            }
            // - C's
            foreach (var c1 in new[] { "d", "k", "ch", "tr" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "t");
                }
            }
            foreach (var c1 in new[] { "sh", "th", "zh", "z", "f", "hh" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "s");
                }
            }
            foreach (var c1 in new[] { "jh", "dr", "b", "g", "t", "dh", "p" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "d");
                }
            }
            foreach (var c1 in new[] { "l", }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "n");
                }
            }
            foreach (var c1 in new[] { "r", }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "er");
                }
            }
            foreach (var c1 in new[] { "v" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "b");
                }
            }
            foreach (var c1 in new[] { "w" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "uw");
                }
            }
            foreach (var c1 in new[] { "y" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "iy");
                }
            }
            foreach (var c1 in new[] { "m", "n", "ng" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(s + " " + c1, s + " " + "n");
                }
            }
            // C -'s
            foreach (var c1 in new[] { "d", "dh", "g", "p" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "b" + " " + s);
                }
            }
            foreach (var c1 in new[] { "jh" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "ch" + " " + s);
                }
            }
            foreach (var c1 in new[] { "b" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "d" + " " + s);
                }
            }
            foreach (var c1 in new[] { "hh", "s" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "f" + " " + s);
                }
            }
            foreach (var c1 in new[] { "ch" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "jh" + " " + s);
                }
            }
            foreach (var c1 in new[] { "t" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "k" + " " + s);
                }
            }
            foreach (var c1 in new[] { "r" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "er" + " " + s);
                }
            }
            foreach (var c1 in new[] { "n" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "m" + " " + s);
                }
            }
            foreach (var c1 in new[] { "l" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "r" + " " + s);
                }
            }
            foreach (var c1 in new[] { "ng", "m" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "n" + " " + s);
                }
            }
            foreach (var c1 in new[] { "sh", "zh", "th", "z", "f" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "s" + " " + s);
                }
            }
            foreach (var c1 in new[] { "k" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "t" + " " + s);
                }
            }
            foreach (var c1 in new[] { "s" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, "z" + " " + s);
                }
            }
            foreach (var c1 in new[] { "hh" }) {
                foreach (var s in new[] { "-" }) {
                    alias = alias.Replace(c1 + " " + s, null);
                }
            }
            //CC's FRONT
            foreach (var c1 in new[] { "ch", "jh", "n", "sh", "t", "v", "ng" }) {
                foreach (var c2 in consonants) {
                        alias = alias.Replace(c1 + " " + c2, c1 + " " + "-");
                }
            }
            foreach (var c1 in new[] { "th" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "-" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "f", "z", "hh" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "s" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "k", "p", "d" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "t" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "dh", "g", "b" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "d" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "l" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "r" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "m" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "n" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "r" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "er" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "s", "zh" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "z" + " " + c2);
                }
            }
            foreach (var c1 in new[] { "r" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c2 + " " + c1, c2 + " " + "er");
                }
            }
            
            return base.ValidateAlias(alias);
            
        }
        protected override double GetTransitionBasicLengthMs(string alias = "") {
            //I wish these were automated instead :')
            double transitionMultiplier = 1.0; // Default multiplier
            bool isEndingConsonant = false;
            bool isEndingVowel = false;
            bool hasCons = false;
            bool hasSuffix = false;
            var excludedVowels = new List<string> { "a", "e", "i", "o", "u" };
            var GlideVCCons = new List<string> { $"{excludedVowels} {connectingGlides}" };
            var NormVCCons = new List<string> { $"{excludedVowels} {connectingNormCons}" };
            var arpabetFirstVDiphthong = new List<string> { "a", "e", "i", "o", "u" };
            var excludedEndings = new List<string> { $"{arpabetFirstVDiphthong}y -", $"{arpabetFirstVDiphthong}w -", $"{arpabetFirstVDiphthong}r -", };
            var numbers = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9" };

            foreach (var c in longConsonants) {
                if (alias.Contains(c) && !alias.StartsWith(c)&& !alias.Contains("ng -")) {
                    return base.GetTransitionBasicLengthMs() * 2.3;
                }
            }

            foreach (var c in normalConsonants) {
                foreach (var v in normalConsonants.Except(GlideVCCons)) {
                    foreach (var b in normalConsonants.Except(NormVCCons)) {
                        if (alias.Contains(c) && !alias.StartsWith(c) &&
                            !alias.Contains("dx") && !alias.Contains($"{c} -")) {
                            if ("l,r,b,d,g,k,p,t,dh".Split(',').Contains(c)) {
                                hasCons = true;
                            } else {
                                return base.GetTransitionBasicLengthMs() * 1.3;
                            }
                        }
                    }
                }
            }

            foreach (var c in connectingNormCons) {
                foreach (var v in vowels.Except(excludedVowels)) {
                    if (alias.Contains(c) && !alias.Contains("- ") && alias.Contains($"{v} {c}")
                       && !alias.Contains("dx")) {
                        return base.GetTransitionBasicLengthMs() * 1.8;
                    }
                }
            }


            foreach (var c in tapConsonant) {
                Console.WriteLine($"c: {c}, alias: {alias}");
                bool shouldTap = alias.Contains(c) || alias.Contains("dx") || alias.EndsWith("dx")
                                    && !alias.Contains('d') && !alias.Contains("dh") && alias.Contains($"{c} dx");
                if (shouldTap) {
                    foreach (var v in vowels) {
                        if (alias.Contains($"{v} dx")) {
                            return base.GetTransitionBasicLengthMs() * 0.5;
                        }
                    }
                }
            }

            foreach (var c in affricates) {
                if (alias.Contains(c) && !alias.StartsWith(c)) {
                    return base.GetTransitionBasicLengthMs() * 1.5;
                }
            }


            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Except(excludedVowels)) {
                    if (alias.Contains($"{v} {c}") && !alias.Contains($"{c} -") && !alias.Contains($"{v} -")) {
                        return base.GetTransitionBasicLengthMs() * 2.5;

                    }
                }
            }

            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Where(v => excludedVowels.Contains(v))) {
                    if (alias.Contains($"{v} r")) {
                        return base.GetTransitionBasicLengthMs() * 0.6;

                    }
                }
            }

            foreach (var c in semilongConsonants) {
                foreach (var v in semilongConsonants.Except(excludedEndings)) {
                    if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -")) {
                        return base.GetTransitionBasicLengthMs() * 1.6;
                    }
                }
            }

            if (hasCons) {
                return base.GetTransitionBasicLengthMs() * 1.3; // Value for 'cons'
            }


            // Check if the alias ends with a consonant or vowel
            foreach (var c in consonants) {
                if (alias.Contains(c) && alias.Contains('-') && alias.StartsWith(c)) {
                    isEndingConsonant = true;
                    break;
                }
            }

            foreach (var v in vowels) {
                if (alias.Contains(v) && alias.Contains('-') && alias.StartsWith(v)) {
                    isEndingVowel = true;
                    break;
                }
            }

            // Check for tone suffix
            foreach (var tone in vowels) {
                if (alias.EndsWith(tone) && alias.Contains($"Bb{numbers}")) {
                    hasSuffix = true;

                    break;
                }
            }
            foreach (var tone in consonants) {
                if (alias.EndsWith(tone) && alias.Contains($"Bb{numbers}")) {
                    hasSuffix = true;

                    break;
                }
            }
            // If the alias ends with a consonant or vowel, return 0.5 ms
            if (isEndingConsonant || isEndingVowel || hasSuffix) {
                return base.GetTransitionBasicLengthMs() * 0.5;
            }


            return base.GetTransitionBasicLengthMs() * transitionMultiplier;
        }
    }
}

