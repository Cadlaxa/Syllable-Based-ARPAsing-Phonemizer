using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using NumSharp.Utilities;
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
        "aam", "am", "axm", "aem", "ahm", "aom", "om", "awm", "aum", "aym", "aim", "ehm", "em", "eym", "eim", "ihm", "iym", "im", "owm", "oum", "oym", "oim", "uhm", "uwm", "um", "oh",
        "eu", "oe", "yw"
        };
        private readonly string[] consonants = "b,ch,d,dh,dr,dx,f,g,hh,jh,k,l,m,n,ng,p,q,r,s,sh,t,th,tr,v,w,y,z,zh".Split(',');
        private readonly string[] affricates = "ch,jh,j".Split(',');
        private readonly string[] tapConsonant = "dx".Split(",");
        private readonly string[] semilongConsonants = "y,w,ng,n,m,v,z,q,hh".Split(",");
        private readonly string[] connectingGlides = "l,r".Split(",");
        private readonly string[] longConsonants = "ch,f,jh,s,sh,th,zh,dr,tr,ts,j,c".Split(",");
        private readonly string[] normalConsonants = "b,d,dh,g,k,p,t,l,r".Split(',');
        private readonly string[] connectingNormCons = "b,d,g,k,p,t,dh".Split(',');
        private readonly Dictionary<string, string> dictionaryReplacements = ("dx=dx;dr=dr;tr=tr").Split(';')
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
        private readonly Dictionary<string, string> missingCphonemes = "nx=n,cl=q,wh=w,dx=d,zh=sh,z=s".Split(',')
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

        // other ARPAbet
        private readonly Dictionary<string, string> otherArpaphonemes = "oh=ao,eu=uh,oe=ax,uy=uw,yw=uw".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isOtherArpaPhonemes = false;

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
            string[] wh = new[] { "wh" };
            string[] c_wy = new[] { "by", "dy", "fy", "gy", "hy", "jy", "ky", "ly", "my", "ny", "py", "ry", "sy", "ty", "vy", "zy",
                                    "bw", "chw", "dw", "fw", "gw", "hw", "jw", "kw", "lw", "mw", "nw", "pw", "rw", "sw", "tw", "vw", "zw",
                                    "bl", "fl", "gl", "kl", "pl", "br", "fr", "gr", "kr", "pr" };
            string[] av_c = new[] { "al", "am", "an", "ang", "ar" };
            string[] ev_c = new[] { "el", "em", "en", "eng" , "err"};
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
                switch (s) {
                    case var str when dr.Contains(str) && !HasOto($"{str} {vowels}", note.tone) && !HasOto($"ay {str}", note.tone):
                        modified.AddRange(new string[] { "jh", s[1].ToString() });
                        break;
                    case var str when tr.Contains(str) && !HasOto($"{str} {vowels}", note.tone) && !HasOto($"ay {str}", note.tone):
                        modified.AddRange(new string[] { "ch", s[1].ToString() });
                        break;
                    case var str when wh.Contains(str) && !HasOto($"{str} {vowels}", note.tone) && !HasOto($"ay {str}", note.tone):
                        modified.AddRange(new string[] { "hh", s[1].ToString() });
                        break;
                    case var str when c_wy.Contains(str) && !HasOto($"{str} {vowels}", note.tone) && !HasOto($"{str}", note.tone):
                        modified.AddRange(new string[] { s[0].ToString(), s[1].ToString() });
                        break;
                    case var str when av_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "aa", s[1].ToString() });
                        break;
                    case var str when ev_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "eh", s[1].ToString() });
                        break;
                    case var str when iv_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "iy", s[1].ToString() });
                        break;
                    case var str when ov_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "ao", s[1].ToString() });
                        break;
                    case var str when uv_c.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { "uw", s[1].ToString() });
                        break;
                    case var str when vowel3S.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { s.Substring(0, 2), s[2].ToString() });
                        break;
                    case var str when vowel4S.Contains(str) && !HasOto($"b {str}", note.tone) && !HasOto(ValidateAlias(str), note.tone):
                        modified.AddRange(new string[] { s.Substring(0, 2), s.Substring(2, 2) });
                        break;
                    default:
                        modified.Add(s);
                        break;
                }
            }
            return modified.ToArray();
        }

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();
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
            // LOAD DICTIONARY FROM FOLDER
            string path = Path.Combine(PluginDir, "arpasing.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, Data.Resources.arpasing_template);
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());
            g2ps.Add(new ArpabetG2p());
            return new G2pFallbacks(g2ps.ToArray());
        }

        protected override List<string> ProcessSyllable(Syllable syllable) {
            string prevV = syllable.prevV;
            string[] cc = syllable.cc;
            string v = syllable.v;
            string basePhoneme;
            var phonemes = new List<string>();
            var lastC = cc.Length - 1;
            var firstC = 0;

            if (!HasOto("b ax", syllable.tone) && !HasOto("ax t", syllable.tone) && !HasOto("ax", syllable.tone)) {
                isMissingVPhonemes = true;
            }
            if (!HasOto("bw", syllable.tone)) {
                isMissingCPhonemes = true;
            }
            if (!HasOto("gcl", syllable.tone)) {
                isTimitPhonemes = true;
            }
            if (!HasOto("b oh", syllable.tone) && !HasOto("ue t", syllable.tone) && !HasOto("oh", syllable.tone)) {
                isOtherArpaPhonemes = true;
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
                        var vc = $"{prevV}{vvExceptions[prevV]}";
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
                // C V OR CV
            } else if (syllable.IsStartingCVWithOneConsonant) {
                var rcv = $"- {cc[0]} {v}";
                var rcv1 = $"- {cc[0]}{v}";
                var crv = $"{cc[0]} {v}";
                var cv = $"{cc[0]}{v}";
                switch (true) {
                    case bool otoCondition1 when HasOto(rcv, syllable.vowelTone) || HasOto(ValidateAlias(rcv), syllable.vowelTone):
                        basePhoneme = rcv;
                        break;
                    case bool otoCondition2 when !HasOto(rcv, syllable.vowelTone) && HasOto(rcv1, syllable.vowelTone):
                        basePhoneme = rcv1;
                        break;
                    case bool otoCondition3 when !HasOto(rcv, syllable.vowelTone) && HasOto(crv, syllable.vowelTone) && HasOto(ValidateAlias(crv), syllable.vowelTone) && !HasOto(rcv1, syllable.vowelTone):
                        basePhoneme = crv;
                        TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}", ValidateAlias($"- {cc[0]}"));
                        break;
                    case bool otoCondition4 when !HasOto(rcv, syllable.vowelTone) && !HasOto(rcv1, syllable.vowelTone) && HasOto(cv, syllable.vowelTone) && HasOto(ValidateAlias(cv), syllable.vowelTone):
                        basePhoneme = cv;
                        TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}", ValidateAlias($"- {cc[0]}"));
                        break;
                    default:
                        basePhoneme = crv;
                        TryAddPhoneme(phonemes, syllable.tone, $"- {cc[0]}", ValidateAlias($"- {cc[0]}"));
                        break;
                }

                // CC V or CCV
            } else if (syllable.IsStartingCVWithMoreThanOneConsonant) {
                var RC = $"- {cc[0]}";
                phonemes.Add(RC);
                if (cc.Length > 2) {
                    for (int i = 1; i < cc.Length - 1; i++) {

                    }
                }
                basePhoneme = $"{cc.Last()} {v}";
                var crv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                switch (true) {
                    case bool condition1 when HasOto(crv, syllable.vowelTone) && (HasOto(basePhoneme, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)):
                        basePhoneme = crv;
                        break;
                    case bool condition2 when !HasOto(crv, syllable.vowelTone) && HasOto(cv, syllable.vowelTone):
                        basePhoneme = cv;
                        break;
                    case bool condition3 when !HasOto(crv, syllable.vowelTone) && !HasOto(cv, syllable.vowelTone):
                        basePhoneme = crv;
                        break;
                    default:
                        basePhoneme = v;
                        break;
                }

            }
            // IS VCV
            else if (syllable.IsVCVWithOneConsonant) {
                for (var i = lastC + 1; i >= 0; i--) {
                    var vr = $"{prevV} -";
                    var vc = $"{prevV} {cc[0]}";
                    if (i == 0) {
                        if (HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone)) {
                            phonemes.Add(vr);
                            phonemes.Add($"- {cc[i]}");
                        }
                    } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                        phonemes.Add(vc);
                        break;
                    } else {
                        continue;
                    }
                }
                basePhoneme = $"{cc[0]} {v}";
                var crv = $"{cc[0]} {v}";
                var cv = $"{cc[0]}{v}";
                switch (true) {
                    case bool condition1 when HasOto(crv, syllable.vowelTone) && (HasOto(basePhoneme, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)):
                        basePhoneme = crv;
                        break;
                    case bool condition2 when !HasOto(crv, syllable.vowelTone) && HasOto(cv, syllable.vowelTone):
                        basePhoneme = cv;
                        break;
                    case bool condition3 when !HasOto(crv, syllable.vowelTone) && !HasOto(cv, syllable.vowelTone):
                        basePhoneme = crv;
                        break;
                    default:
                        basePhoneme = v;
                        break;
                }
            } else {
                // IS VCV WITH MORE THAN ONE CONSONANT
                for (var i = lastC + 1; i >= 0; i--) {
                    var vr = $"{prevV} -";
                    var vc = $"{prevV} {cc[0]}";
                    if (i == 0) {
                        if (HasOto(vr, syllable.tone) || HasOto(ValidateAlias(vr), syllable.tone)) {
                            phonemes.Add(vr);
                            phonemes.Add($"- {cc[i]}");
                        }
                    } else if (HasOto(vc, syllable.tone) || HasOto(ValidateAlias(vc), syllable.tone)) {
                        phonemes.Add(vc);
                        break;
                    } else {
                        continue;
                    }
                }
                basePhoneme = $"{cc.Last()} {v}";
                var crv = $"{cc.Last()} {v}";
                var cv = $"{cc.Last()}{v}";
                switch (true) {
                    case bool condition1 when HasOto(crv, syllable.vowelTone) && (HasOto(basePhoneme, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)):
                        basePhoneme = crv;
                        break;
                    case bool condition2 when !HasOto(crv, syllable.vowelTone) && HasOto(cv, syllable.vowelTone):
                        basePhoneme = cv;
                        break;
                    case bool condition3 when !HasOto(crv, syllable.vowelTone) && !HasOto(cv, syllable.vowelTone):
                        basePhoneme = crv;
                        break;
                    default:
                        basePhoneme = v;
                        break;
                }
                for (int i = 0; i < cc.Length - 1; i++) {
                    var currentcc = $"{cc[i]} {cc[i + 1]}";

                }
            }
            for (var i = firstC; i < lastC; i++) {
                var rccv = $"- {string.Join("", cc)} {v}";
                var cc1 = $"{string.Join(" ", cc.Skip(i))}";
                var crv = $"{cc.Last()} {v}";
                if (!HasOto(rccv, syllable.vowelTone)) {
                    if (!HasOto(cc1, syllable.tone)) {
                        // [C1] [C2]
                        cc1 = $"{cc[i]} {cc[i + 1]}";
                        // CHECK FOR Y'S AND W'S
                        if (i + 2 < cc.Length && (cc[i] == "y" || cc[i] == "w")) {
                            cc1 = $"{cc[i]} {cc[i + 1]} {cc[i + 2]}";
                            i += 2;
                        }
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    // CC FALLBACKS
                    if (!HasOto(cc1, syllable.tone) && !HasOto($"{cc[i]} {cc[i + 1]}", syllable.tone)) {
                        // [C1 -] [- C2]
                        cc1 = $"- {cc[i + 1]}";
                        phonemes.Add($"{cc[i]} -");
                    }
                    if (!HasOto(cc1, syllable.tone)) {
                        cc1 = ValidateAlias(cc1);
                    }
                    if (HasOto(crv, syllable.vowelTone)) {
                        basePhoneme = crv;
                    }
                    if (i + 1 < lastC) {
                        var cc2 = $"{string.Join("", cc.Skip(i))}";
                        if (!HasOto(cc2, syllable.tone)) {
                            // [C1] [C2]
                            cc2 = $"{cc[i]} {cc[i + 1]}";
                            // CHECK FOR Y'S AND W'S
                            if (i + 2 < cc.Length && (cc[i] == "y" || cc[i] == "w")) {
                                cc2 = $"{cc[i]} {cc[i + 1]} {cc[i + 2]}";
                                i += 2;
                            }
                        }
                        if (!HasOto(cc1, syllable.tone)) {
                            cc1 = ValidateAlias(cc1);
                        }
                        if (!HasOto(cc2, syllable.tone)) {
                            cc2 = $"{cc[i + 1]} {cc[i + 2]}";
                        }
                        if (!HasOto(cc2, syllable.tone)) {
                            cc2 = ValidateAlias(cc2);
                        }
                        if (HasOto(crv, syllable.vowelTone)) {
                            basePhoneme = crv;
                        }
                        if (TryAddPhoneme(phonemes, syllable.tone, $"{cc[i]}{cc[i + 1]}{cc[i + 2]} -")) {
                            // if it exists, use [C1][C2][C3] -
                            i += 2;
                        } else if (HasOto(cc1, syllable.tone) && HasOto(cc2, syllable.tone)) {
                            // like [V C1] [C1 C2] [C2 C3] [C3 ..]
                            phonemes.Add(cc1);
                        } else if (TryAddPhoneme(phonemes, syllable.tone, cc1)) {
                            // like [V C1] [C1 C2] [C2 ..]
                        }
                    } else {
                        TryAddPhoneme(phonemes, syllable.tone, cc1);
                    }
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
            if (alias == "dx ax") {
                return alias.Replace("ax", "ah");
            } else if (alias == "ng ae") {
                return alias.Replace("ng", "n");
            } else if (alias == "ng ao") {
                return alias.Replace("ao", "ow");
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
                return alias.Replace("v", "b");
            } else if (alias == "z ao") {
                return alias.Replace("z", "s");
            } else if (alias == "ng eh") {
                return alias.Replace("ng", "n");
            } else if (alias == "z eh") {
                return alias.Replace("z", "s");
            } else if (alias == "jh er") {
                return alias.Replace("jh", "z");
            } else if (alias == "ng er") {
                return alias.Replace("ng", "n");
            } else if (alias == "r er") {
                return alias.Replace("r er", "er");
            } else if (alias == "th er") {
                return alias.Replace("th er", "th r");
            } else if (alias == "jh ey") {
                return alias.Replace("ey", "ae");
            } else if (alias == "ng ey") {
                return alias.Replace("ng", "n");
            } else if (alias == "th ey") {
                return alias.Replace("ey", "ae");
            } else if (alias == "zh ey") {
                return alias.Replace("zh ey", "jh ae");
            } else if (alias == "ch ow") {
                return alias.Replace("ch", "sh");
            } else if (alias == "jh ow") {
                return alias.Replace("ow", "oy");
            } else if (alias == "v ow") {
                return alias.Replace("v", "b");
            } else if (alias == "th ow") {
                return alias.Replace("th", "s");
            } else if (alias == "z ow") {
                return alias.Replace("z", "s");
            } else if (alias == "ch oy") {
                return alias.Replace("ch oy", "sh ow");
            } else if (alias == "th oy") {
                return alias.Replace("th oy", "s ao");
            } else if (alias == "v oy") {
                return alias.Replace("v", "b");
            } else if (alias == "w oy") {
                return alias.Replace("oy", "ao");
            } else if (alias == "z oy") {
                return alias.Replace("oy", "aa");
            } else if (alias == "ch uh") {
                return alias.Replace("ch", "sh");
            } else if (alias == "dh uh") {
                return alias.Replace("dh uh", "d uw");
            } else if (alias == "jh uh") {
                return alias.Replace("uh", "uw");
            } else if (alias == "ng uh") {
                return alias.Replace("ng", "n");
            } else if (alias == "th uh") {
                return alias.Replace("th uh", "f uw");
            } else if (alias == "v uh") {
                return alias.Replace("v", "b");
            } else if (alias == "z uh") {
                return alias.Replace("z", "s");
            } else if (alias == "ch uw") {
                return alias.Replace("ch", "sh");
            } else if (alias == "dh uw") {
                return alias.Replace("dh", "d");
            } else if (alias == "g uw") {
                return alias.Replace("g", "k");
            } else if (alias == "jh uw") {
                return alias.Replace("jh", "sh");
            } else if (alias == "ng uw") {
                return alias.Replace("ng", "n");
            } else if (alias == "th uw") {
                return alias.Replace("th uw", "f uw");
            } else if (alias == "v uw") {
                return alias.Replace("v", "b");
            } else if (alias == "z uw") {
                return alias.Replace("z", "s");
            } else if (alias == "zh aa") {
                return alias.Replace("zh", "sh");
            } else if (alias == "zh ao") {
                return alias.Replace("zh", "sh");
            } else if (alias == "zh ae") {
                return alias.Replace("zh ae", "sh ah");
            } else if (alias == "ng oy") {
                return alias.Replace("oy", "ow");
            } else if (alias == "sh ao") {
                return alias.Replace("ao", "ow");
            } else if (alias == "z uh") {
                return alias.Replace("z uh", "s uw");
            }

            // VALIDATE ALIAS DEPENDING ON METHOD
            if (isMissingVPhonemes || isMissingCPhonemes || isTimitPhonemes || isOtherArpaPhonemes) {
                foreach (var syllable in missingVphonemes.Concat(missingCphonemes).Concat(timitphonemes
                    .Concat(otherArpaphonemes))) {
                    alias = alias.Replace(syllable.Key, syllable.Value);
                }
            }

            var replacements = new Dictionary<string, string[]> {
                    { "ao", new[] { "ow" } },
                    { "ax", new[] { "ah" } },
                    { "oy", new[] { "ow" } },
                    { "aw", new[] { "ah" } },
                    { "ay", new[] { "ah" } },
                    { "eh", new[] { "ae" } },
                    { "ey", new[] { "eh" } },
                    { "ow", new[] { "ao" } },
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
            { "ey eh", new List<string> { "y eh" } },
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
            if (alias == "aw jh") {
                return alias.Replace("jh", "d");
            }
            if (alias == "aw ng") {
                return alias.Replace("aw ng", "uh ng");
            }
            if (alias == "aw q") {
                return alias.Replace("q", "t");
            }
            if (alias == "aw zh") {
                return alias.Replace("zh", "d");
            }
            if (alias == "aw w") {
                return alias.Replace("aw", "ah");
            }

            //VC (ay specific)
            if (alias == "ay ng") {
                return alias.Replace("ay", "ih");
            }
            if (alias == "ay q") {
                return alias.Replace("q", "t");
            }
            if (alias == "ay zh") {
                return alias.Replace("zh", "jh");
            }
            //VC (ey specific)
            if (alias == "ey ng") {
                return alias.Replace("ey", "ih");
            }
            if (alias == "ey q") {
                return alias.Replace("q", "t");
            }
            if (alias == "ey zh") {
                return alias.Replace("zh", "jh");
            }
            //VC (ow specific)
            if (alias == "ow ch") {
                return alias.Replace("ch", "t");
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
            //VC (oy specific)
            if (alias == "- oy") {
                return alias.Replace("oy", "ow");
            }
            if (alias == "oy f") {
                return alias.Replace("oy", "ih");
            }
            if (alias == "oy ng") {
                return alias.Replace("iy", "ih");
            }
            if (alias == "oy q") {
                return alias.Replace("oy q", "iy t");
            }
            if (alias == "oy zh") {
                return alias.Replace("oy zh", "iy jh");

            }

            // VC (aa)
            if (alias == "aa b") {
                alias = alias.Replace(alias, "aa d");
            } else if (alias == "aa q") {
                alias = alias.Replace(alias, "aa t");
            } else if (alias == "aa y") {
                alias = alias.Replace(alias, "ah iy");
            } else if (alias == "aa zh") {
                alias = alias.Replace(alias, "aa z");
            }

            // alias (ae specific)
              else if (alias == "ae b") {
                alias = alias.Replace(alias, "ah d");
            } else if (alias == "ae q") {
                alias = alias.Replace(alias, "ah t");
            } else if (alias == "ae y") {
                alias = alias.Replace(alias, "ah iy");
            } else if (alias == "ae zh") {
                alias = alias.Replace(alias, "ah z");
            }

            // alias (ah specific)
            else if (alias == "ah b") {
                alias = alias.Replace(alias, "ah d");
            } else if (alias == "ah q") {
                alias = alias.Replace(alias, "ah t");
            } else if (alias == "ah y") {
                alias = alias.Replace(alias, "ah iy");
            } else if (alias == "ah zh") {
                alias = alias.Replace(alias, "ah z");
            }

              // alias (ao)
              // alias (ao specific)
            else if (alias == "ao b") {
                alias = alias.Replace(alias, "ah d");
            } else if (alias == "ao q") {
                alias = alias.Replace(alias, "ah t");
            } else if (alias == "ao y") {
                alias = alias.Replace(alias, "ow y");
            } else if (alias == "ao zh") {
                alias = alias.Replace(alias, "ah z");
            }

              // alias (ax)
              // alias (ax specific)
            else if (alias == "ax b") {
                alias = alias.Replace(alias, "ah d");
            } else if (alias == "ax q") {
                alias = alias.Replace(alias, "ah t");
            } else if (alias == "ax y") {
                alias = alias.Replace(alias, "ah iy");
            } else if (alias == "ax zh") {
                alias = alias.Replace(alias, "ah z");
            }

            // alias (eh)
            // alias (eh specific)
            else if (alias == "eh b") {
                alias = alias.Replace(alias, "eh d");
            } else if (alias == "eh ch") {
                alias = alias.Replace(alias, "eh t");
            } else if (alias == "eh ng") {
                alias = alias.Replace(alias, "eh n");
            } else if (alias == "eh q") {
                alias = alias.Replace(alias, "eh t");
            } else if (alias == "eh y") {
                alias = alias.Replace(alias, "ey");
            } else if (alias == "eh zh") {
                alias = alias.Replace(alias, "eh s");
            }

              // alias (er specific)
            else if (alias == "er ch") {
                alias = alias.Replace(alias, "er t");
            } else if (alias == "er jh") {
                alias = alias.Replace(alias, "er d");
            } else if (alias == "er ng") {
                alias = alias.Replace(alias, "er n");
            } else if (alias == "er q") {
                alias = alias.Replace(alias, "er t");
            } else if (alias == "er r") {
                alias = alias.Replace(alias, "er");
            } else if (alias == "er sh") {
                alias = alias.Replace(alias, "er s");
            } else if (alias == "er zh") {
                alias = alias.Replace(alias, "er z");
            }

            // alias (ih specific)
            else if (alias == "ih b") {
                alias = alias.Replace(alias, "ih d");
            } else if (alias == "ih hh") {
                alias = alias.Replace(alias, "iy hh");
            } else if (alias == "ih q") {
                alias = alias.Replace(alias, "ih t");
            } else if (alias == "ih w") {
                alias = alias.Replace(alias, "iy w");
            } else if (alias == "ih y") {
                alias = alias.Replace(alias, "iy y");
            } else if (alias == "ih zh") {
                alias = alias.Replace(alias, "ih z");
            }

            // alias (iy specific)
            else if (alias == "iy f") {
                alias = alias.Replace(alias, "iy hh");
            } else if (alias == "iy n") {
                alias = alias.Replace(alias, "iy m");
            } else if (alias == "iy ng") {
                alias = alias.Replace(alias, "ih ng");
            } else if (alias == "iy q") {
                alias = alias.Replace(alias, "iy t");
            } else if (alias == "iy zh") {
                alias = alias.Replace(alias, "iy z");
            }

            // alias (uh)
            // alias (uh specific)
            else if (alias == "uh ch") {
                alias = alias.Replace(alias, "uh t");
            } else if (alias == "uh jh") {
                alias = alias.Replace(alias, "uw d");
            } else if (alias == "uh q") {
                alias = alias.Replace(alias, "uh t");
            } else if (alias == "uh zh") {
                alias = alias.Replace(alias, "uw z");
            }

            // alias (uw specific)
            else if (alias == "uw ch") {
                alias = alias.Replace(alias, "uw t");
            } else if (alias == "uw jh") {
                alias = alias.Replace(alias, "uw d");
            } else if (alias == "uw ng") {
                alias = alias.Replace(alias, "uw n");
            } else if (alias == "uw q") {
                alias = alias.Replace(alias, "uw t");
            } else if (alias == "uw zh") {
                alias = alias.Replace(alias, "uw z");
            }

            //CC (b)
            //CC (b specific)
            else if (alias == "b ch") {
                return alias.Replace("b ch", "t ch");
            } else if (alias == "b dh") {
                return alias.Replace("b ch", "p dh");
            } else if (alias == "b ng") {
                return alias.Replace("b ng", "ng");
            } else if (alias == "b th") {
                return alias.Replace("b th", "t th");
            } else if (alias == "b zh") {
                return alias.Replace("zh", "z");
            }

            //CC (ch specelse ific)
            else if (alias == "ch r") {
                return alias.Replace("ch r", "ch er");
            } else if (alias == "ch w") {
                return alias.Replace("ch w", "ch ah");
            } else if (alias == "ch y") {
                return alias.Replace("ch y", "ch iy");
            } else if (alias == "ch -") {
                return alias.Replace("ch", "jh");
            } else if (alias == "- ch") {
                return alias.Replace("ch", "jh");
            }

            //CC (d specelse ific)
            else if (alias == "d ch") {
                return alias.Replace("d", "t");
            } else if (alias == "d ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "d th") {
                return alias.Replace("d th", "t th");
            } else if (alias == "d zh") {
                return alias.Replace("zh", "z");
            }

            //CC (dh specelse ific)
            else if (alias == "dh ch") {
                return alias.Replace("dh ch", "t ch");
            } else if (alias == "dh dh") {
                return alias.Replace("dh dh", "dh d");
            } else if (alias == "dh ng") {
                return alias.Replace("dh ng", "d n");
            } else if (alias == "dh zh") {
                return alias.Replace("zh", "z");
            }


            //CC (f specelse ific)
            else if (alias == "f sh") {
                return alias.Replace("sh", "s");
            } else if (alias == "f w") {
                return alias.Replace("f w", "f uw");
            } else if (alias == "f z") {
                return alias.Replace("z", "s");
            } else if (alias == "f zh") {
                return alias.Replace("zh", "s");
            } else if (alias == "f -") {
                return alias.Replace("f", "th");
            }

            //CC (g specelse ific)
            else if (alias == "g ch") {
                return alias.Replace("g ch", "t ch");
            } else if (alias == "g dh") {
                return alias.Replace("g", "d");
            } else if (alias == "g ng") {
                return alias.Replace("g ng", "ng");
            } else if (alias == "g zh") {
                return alias.Replace("zh", "z");
            }

            //CC (hh specelse ific)
            else if (alias == "hh f") {
                return alias.Replace("hh", "f");
            } else if (alias == "hh l") {
                return alias.Replace("hh", "f");
            } else if (alias == "hh ng") {
                return alias.Replace("hh ng", "s n");
            } else if (alias == "hh r") {
                return alias.Replace("hh", "f");
            } else if (alias == "hh s") {
                return alias.Replace("hh", "f");
            } else if (alias == "hh sh") {
                return alias.Replace("hh sh", "s s");
            } else if (alias == "hh t") {
                return alias.Replace("hh t", "f");
            } else if (alias == "hh th") {
                return alias.Replace("hh th", "hh");
            } else if (alias == "hh w") {
                return alias.Replace("hh w", "hh uw");
            } else if (alias == "hh y") {
                return alias.Replace("hh", "f");
            } else if (alias == "hh z") {
                return alias.Replace("hh z", "s s");
            } else if (alias == "hh -") {
                return alias.Replace("hh -", null);
            }

            //CC (jh specelse ific)
            else if (alias == "jh hh") {
                return alias.Replace("jh", "s");
            } else if (alias == "jh l") {
                return alias.Replace("jh", "f");
            } else if (alias == "jh m") {
                return alias.Replace("jh", "s");
            } else if (alias == "jh n") {
                return alias.Replace("jh", "s");
            } else if (alias == "jh ng") {
                return alias.Replace("jh ng", "s n");
            } else if (alias == "jh r") {
                return alias.Replace("jh r", "jh ah");
            } else if (alias == "jh s") {
                return alias.Replace("jh", "f");
            } else if (alias == "jh w") {
                return alias.Replace("jh w", "jh ah");
            } else if (alias == "jh y") {
                return alias.Replace("y", "iy");
            }

            //CC (k specelse ific)
            else if (alias == "k z") {
                return alias.Replace("z", "s");
            } else if (alias == "k zh") {
                return alias.Replace("zh", "s");
            }

            //CC (l specelse ific)
            else if (alias == "l ch") {
                return alias.Replace("ch", "t");
            } else if (alias == "l b") {
                return alias.Replace("l", "d");
            } else if (alias == "l hh") {
                return alias.Replace("l", "r");
            } else if (alias == "l jh") {
                return alias.Replace("jh", "d");
            } else if (alias == "l ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "l sh") {
                return alias.Replace("sh", "s");
            } else if (alias == "l th") {
                return alias.Replace("l th", "l s");
            } else if (alias == "l zh") {
                return alias.Replace("zh", "z");
            }

            //CC (m specelse ific)
            else if (alias == "m ch") {
                return alias.Replace("m", "n");
            } else if (alias == "m hh") {
                return alias.Replace("m hh", "hh");
            } else if (alias == "m jh") {
                return alias.Replace("jh", "d");
            } else if (alias == "m ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "m n") {
                return alias.Replace("m n", "n");
            } else if (alias == "m m") {
                return alias.Replace("m m", "n");
            } else if (alias == "m r") {
                return alias.Replace("m", "n");
            } else if (alias == "m s") {
                return alias.Replace("m", "n");
            } else if (alias == "m sh") {
                return alias.Replace("m", "n");
            } else if (alias == "m v") {
                return alias.Replace("m v", "m m");
            } else if (alias == "m zh") {
                return alias.Replace("zh", "z");
            }

            //CC (n specelse ific)
            else if (alias == "n ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "n n") {
                return alias.Replace("n n", "n");
            } else if (alias == "n m") {
                return alias.Replace("n m", "n");
            } else if (alias == "n v") {
                return alias.Replace("n v", "n m");
            } else if (alias == "n zh") {
                return alias.Replace("zh", "z");
            }

            //CC (ng)
            foreach (var c1 in new[] { "ng" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "n" + " " + c2);
                }
            }

            //CC (ng specelse ific)
            if (alias == "ng ch") {
                return alias.Replace("ch", "t");
            } else if (alias == "ng ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "ng v") {
                return alias.Replace("ng v", "ng s");
            } else if (alias == "ng zh") {
                return alias.Replace("zh", "z");
            }

              //CC (p specelse ific)
              else if (alias == "p dx") {
                return alias.Replace("p dx", "t d");
            } else if (alias == "p z") {
                return alias.Replace("z", "s");
            } else if (alias == "p zh") {
                return alias.Replace("zh", "s");
            }
            //CC (q)
            foreach (var c1 in new[] { "q" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "-" + " " + c2);
                }
            }

            //CC (r specelse ific)
            if (alias == "r ch") {
                return alias.Replace("ch", "t");
            } else if (alias == "r dr") {
                return alias.Replace("dr", "jh");
            } else if (alias == "r dx") {
                return alias.Replace("dx", "d");
            } else if (alias == "r ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "r sh") {
                return alias.Replace("sh", "s");
            } else if (alias == "r zh") {
                return alias.Replace("zh", "z");
            }

            //CC (s specelse ific)
            else if (alias == "s dr") {
                return alias.Replace("dr", "jh");
            } else if (alias == "s ch") {
                return alias.Replace("ch", "t");
            } else if (alias == "s dx") {
                return alias.Replace("dx", "d");
            } else if (alias == "s ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "s sh") {
                return alias.Replace("sh", "s");
            } else if (alias == "s th") {
                return alias.Replace("s", "z");
            } else if (alias == "s v") {
                return alias.Replace("s", "z");
            } else if (alias == "s zh") {
                return alias.Replace("zh", "s");
            }

            //CC (sh specelse ific)
            else if (alias == "sh f") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh hh") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh l") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh m") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh n") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh ng") {
                return alias.Replace("sh ng", "s n");
            } else if (alias == "sh r") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh s") {
                return alias.Replace("sh", "s");
            } else if (alias == "sh sh") {
                return alias.Replace("sh sh", "s s");
            } else if (alias == "sh th") {
                return alias.Replace("sh th", "th");
            } else if (alias == "sh w") {
                return alias.Replace("sh w", "sh uw");
            } else if (alias == "sh y") {
                return alias.Replace("sh y", "sh iy");
            } else if (alias == "sh z") {
                return alias.Replace("sh z", "s s");
            }

            //CC (t specelse ific)
            else if (alias == "t y") {
                return alias.Replace("y", "iy");
            } else if (alias == "t z") {
                return alias.Replace("t", "g");
            } else if (alias == "t zh") {
                return alias.Replace("t zh", "g z");
            }

            //CC (th specelse ific)
            else if (alias == "th dr") {
                return alias.Replace("th dr", "s jh");
            } else if (alias == "th y") {
                return alias.Replace("th y", "th ih");
            } else if (alias == "th zh") {
                return alias.Replace("zh", "s");
            }

            //CC (v specelse ific)
            else if (alias == "v dh") {
                return alias.Replace("dh", "d");
            } else if (alias == "v f") {
                return alias.Replace("v", "s");
            } else if (alias == "v hh") {
                return alias.Replace("v", "s");
            } else if (alias == "v l") {
                return alias.Replace("v", "s");
            } else if (alias == "v m") {
                return alias.Replace("v", "s");
            } else if (alias == "v n") {
                return alias.Replace("v", "s");
            } else if (alias == "v ng") {
                return alias.Replace("v ng", "s n");
            } else if (alias == "v r") {
                return alias.Replace("v", "s");
            } else if (alias == "v th") {
                return alias.Replace("v th", "th");
            } else if (alias == "v s") {
                return alias.Replace("v", "s");
            } else if (alias == "v sh") {
                return alias.Replace("v sh", "s s");
            } else if (alias == "v w") {
                return alias.Replace("v", "s");
            } else if (alias == "v y") {
                return alias.Replace("v", "s");
            } else if (alias == "v z") {
                return alias.Replace("v z", "s s");
            }

            //CC (w specelse ific)
            foreach (var c1 in new[] { "w" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "uw" + " " + c2);
                }
            }
            if (alias == "w -") {
                return alias.Replace("w", "uw");
            }

            //CC (y specelse ific)
            foreach (var c1 in new[] { "y" }) {
                foreach (var c2 in consonants) {
                    alias = alias.Replace(c1 + " " + c2, "iy" + " " + c2);
                }
            }
            if (alias == "y -") {
                return alias.Replace("y", "iy");
            }

//CC (z specelse ific)
else if (alias == "z ch") {
                return alias.Replace("ch", "t");
            } else if (alias == "z dr") {
                return alias.Replace("dr", "jh");
            } else if (alias == "z dx") {
                return alias.Replace("dx", "d");
            } else if (alias == "z tr") {
                return alias.Replace("tr", "t");
            } else if (alias == "z ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "z z") {
                return alias.Replace("z z", "z s");
            } else if (alias == "z zh") {
                return alias.Replace("z zh", "z s");
            }
              //CC (zh)
              //CC (zh specelse ific)
              else if (alias == "zh ch") {
                return alias.Replace("ch", "t");
            } else if (alias == "zh dr") {
                return alias.Replace("dr", "jh");
            } else if (alias == "zh dx") {
                return alias.Replace("dx", "d");
            } else if (alias == "zh tr") {
                return alias.Replace("tr", "t");
            } else if (alias == "zh ng") {
                return alias.Replace("ng", "n");
            } else if (alias == "zh z") {
                return alias.Replace("zh z", "z s");
            } else if (alias == "zh zh") {
                return alias.Replace("z zh", "z s");
            }
            //VC's
            foreach (var v1 in new[] { "aw", "ow", "uh" }) {
                foreach (var c1 in consonants) {
                    var substringToReplace = v1 + " " + c1;
                    if (alias.Contains(substringToReplace)) {
                        alias = alias.Replace(substringToReplace, "uw" + " " + c1);
                    }
                }
            }
            foreach (var v1 in new[] { "ay", "ey", "oy" }) {
                foreach (var c1 in consonants) {
                    var substringToReplace = v1 + " " + c1;
                    if (alias.Contains(substringToReplace)) {
                        alias = alias.Replace(substringToReplace, "iy" + " " + c1);
                    }
                }
            }
            foreach (var v1 in new[] { "aa", "ae", "ao", "eh", "er" }) {
                foreach (var c1 in consonants) {
                    var substringToReplace = v1 + " " + c1;
                    if (alias.Contains(substringToReplace)) {
                        alias = alias.Replace(substringToReplace, "ah" + " " + c1);
                    }
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
            foreach (var c1 in new[] { "d", "k", "ch", "tr", "sh", "th", "zh", "z", "f", "hh", "jh", "dr", "b", "g", "t", "dh", "p", "l", "r", "v", "w", "y", "m", "n", "ng" }) {
                foreach (var s in new[] { "-" }) {
                    switch (s + " " + c1) {
                        case var str when alias.Contains(str):
                            if (c1 == "d" || c1 == "k" || c1 == "ch" || c1 == "tr") {
                                alias = alias.Replace(str, s + " " + "t");
                            } else if (c1 == "sh" || c1 == "th" || c1 == "zh" || c1 == "z" || c1 == "f" || c1 == "hh") {
                                alias = alias.Replace(str, s + " " + "s");
                            } else if (c1 == "jh" || c1 == "dr" || c1 == "b" || c1 == "g" || c1 == "t" || c1 == "dh" || c1 == "p") {
                                alias = alias.Replace(str, s + " " + "d");
                            } else if (c1 == "l") {
                                alias = alias.Replace(str, s + " " + "n");
                            } else if (c1 == "r") {
                                alias = alias.Replace(str, s + " " + "er");
                            } else if (c1 == "v") {
                                alias = alias.Replace(str, s + " " + "b");
                            } else if (c1 == "w") {
                                alias = alias.Replace(str, s + " " + "uw");
                            } else if (c1 == "y") {
                                alias = alias.Replace(str, s + " " + "iy");
                            } else if (c1 == "m" || c1 == "n" || c1 == "ng") {
                                alias = alias.Replace(str, s + " " + "n");
                            }
                            break;
                    }
                }
            }
            // C -'s
            foreach (var c1 in new[] { "d", "dh", "g", "p", "jh", "b", "s", "ch", "t", "r", "n", "l", "ng", "sh", "zh", "th", "z", "f", "k", "s", "hh" }) {
                foreach (var s in new[] { "-" }) {
                    switch (c1 + " " + s) {
                        case var str when alias.Contains(str):
                            if (c1 == "d" || c1 == "dh" || c1 == "g" || c1 == "p") {
                                alias = alias.Replace(str, "b" + " " + s);
                            } else if (c1 == "jh") {
                                alias = alias.Replace(str, "ch" + " " + s);
                            } else if (c1 == "b") {
                                alias = alias.Replace(str, "d" + " " + s);
                            } else if (c1 == "s") {
                                alias = alias.Replace(str, "f" + " " + s);
                            } else if (c1 == "ch") {
                                alias = alias.Replace(str, "jh" + " " + s);
                            } else if (c1 == "t") {
                                alias = alias.Replace(str, "k" + " " + s);
                            } else if (c1 == "r") {
                                alias = alias.Replace(str, "er" + " " + s);
                            } else if (c1 == "n") {
                                alias = alias.Replace(str, "m" + " " + s);
                            } else if (c1 == "l") {
                                alias = alias.Replace(str, "r" + " " + s);
                            } else if (c1 == "ng" || c1 == "m") {
                                alias = alias.Replace(str, "n" + " " + s);
                            } else if (c1 == "sh" || c1 == "zh" || c1 == "th" || c1 == "z" || c1 == "f") {
                                alias = alias.Replace(str, "s" + " " + s);
                            } else if (c1 == "k") {
                                alias = alias.Replace(str, "t" + " " + s);
                            } else if (c1 == "s") {
                                alias = alias.Replace(str, "z" + " " + s);
                            } else if (c1 == "hh") {
                                alias = alias.Replace(str, null);
                            }
                            break;
                    }
                }
            }
            // CC's
            foreach (var c1 in new[] { "f", "z", "hh", "k", "p", "d", "dh", "g", "b", "m", "r" }) {
                foreach (var c2 in consonants) {
                    switch (c1 + " " + c2) {
                        case var str when alias.Contains(str):
                            if (c1 == "f" || c1 == "z" || c1 == "hh") {
                                alias = alias.Replace(str, "s" + " " + c2);
                            } else if (c1 == "k" || c1 == "p" || c1 == "d") {
                                alias = alias.Replace(str, "t" + " " + c2);
                            } else if (c1 == "dh" || c1 == "g" || c1 == "b") {
                                alias = alias.Replace(str, "d" + " " + c2);
                            } else if (c1 == "m") {
                                alias = alias.Replace(str, "n" + " " + c2);
                            } else if (c1 == "r") {
                                alias = alias.Replace(str, c2 + " " + "er");
                            }
                            break;
                    }
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
            bool haslr = false;
            bool hasSuffix = false;
            var excludedVowels = new List<string> { "a", "e", "i", "o", "u" };
            var GlideVCCons = new List<string> { $"{excludedVowels} {connectingGlides}" };
            var NormVCCons = new List<string> { $"{excludedVowels} {connectingNormCons}" };
            var arpabetFirstVDiphthong = new List<string> { "a", "e", "i", "o", "u" };
            var excludedEndings = new List<string> { $"{arpabetFirstVDiphthong}y -", $"{arpabetFirstVDiphthong}w -", $"{arpabetFirstVDiphthong}r -", };
            var numbers = new List<string> { "1", "2", "3", "4", "5", "6", "7", "8", "9" };


            foreach (var c in longConsonants) {
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains("ng -")) {
                    return base.GetTransitionBasicLengthMs() * 2.3;
                }
            }

            foreach (var c in normalConsonants) {
                foreach (var v in normalConsonants.Except(GlideVCCons)) {
                    foreach (var b in normalConsonants.Except(NormVCCons)) {
                        if (alias.Contains(c) && !alias.StartsWith(c) &&
                        !alias.Contains("dx") && !alias.Contains($"{c} -")) {
                            if ("b,d,g,k,p,t,dh".Split(',').Contains(c)) {
                                hasCons = true;
                            } else if ("l,r".Split(',').Contains(c)) {
                                haslr = true;
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
                        return base.GetTransitionBasicLengthMs() * 2.0;
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
            } else if (haslr) {
                return base.GetTransitionBasicLengthMs() * 1.7; // Value for 'cons'
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

