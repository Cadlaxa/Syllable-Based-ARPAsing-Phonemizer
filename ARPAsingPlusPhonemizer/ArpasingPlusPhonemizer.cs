using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Melanchall.DryWetMidi.Composing;
using Melanchall.DryWetMidi.MusicTheory;
using OpenUtau.Api;
using OpenUtau.Classic;
using OpenUtau.Core.G2p;
using OpenUtau.Core.Ustx;
using Serilog;
using YamlDotNet.Core.Tokens;

namespace OpenUtau.Plugin.Builtin {
    [Phonemizer("Arpasing+ Phonemizer", "EN ARPA+", "Cadlaxa", language: "EN")]
    // Custom ARPAsing Phonemizer for OU
    // main focus of this Phonemizer is to bring fallbacks to existing available phonemes from
    // ARPAsing 0.1.0 and 0.2.0 banks
    public class ArpasingPlusPhonemizer : SyllableBasedPhonemizer {
        protected IG2p g2p;
        private readonly string[] vowels = {
        "aa", "ax", "ae", "ah", "ao", "aw", "ay", "eh", "er", "ey", "ih", "iy", "ow", "oy", "uh", "uw", "a", "e", "i", "o", "u", "ai", "ei", "oi", "au", "ou",
        "aar", "ar", "axr", "aer", "ahr", "aor", "or", "awr", "aur", "ayr", "air", "ehr", "eyr", "eir", "ihr", "iyr", "ir", "owr", "our", "oyr", "oir", "uhr", "uwr", "ur",
        "aal", "al", "axl", "ael", "ahl", "aol", "ol", "awl", "aul", "ayl", "ail", "ehl", "el", "eyl", "eil", "ihl", "iyl", "il", "owl", "oul", "oyl", "oil", "uhl", "uwl", "ul",
        "naan", "an", "axn", "aen", "ahn", "aon", "on", "awn", "aun", "ayn", "ain", "ehn", "en", "eyn", "ein", "ihn", "iyn", "in", "own", "oun", "oyn", "oin", "uhn", "uwn", "un",
        "aang", "ang", "axng", "aeng", "ahng", "aong", "ong", "awng", "aung", "ayng", "aing", "ehng", "eng", "eyng", "eing", "ihng", "iyng", "ing", "owng", "oung", "oyng", "oing", "uhng", "uwng", "ung",
        "aam", "am", "axm", "aem", "ahm", "aom", "om", "awm", "aum", "aym", "aim", "ehm", "em", "eym", "eim", "ihm", "iym", "im", "owm", "oum", "oym", "oim", "uhm", "uwm", "um"
        };
        private readonly string[] consonants = "b,ch,d,dh,dr,dx,f,g,hh,jh,k,l,m,n,ng,p,q,r,s,sh,t,th,tr,v,w,y,z,zh".Split(',');
        private readonly string[] affricates = "ch,jh,j".Split(',');
        private readonly string[] tapConsonant = "dx".Split(",");
        private readonly string[] semilongConsonants = "y,w,ng,n,m,v,z,q,hh".Split(",");
        private readonly string[] connectingGlides = "l,r".Split(",");
        private readonly string[] longConsonants = "ch,f,jh,s,sh,th,zh,dr,tr,ts,j".Split(",");
        private readonly string[] normalConsonants = "b,d,dh,g,k,p,t,l,r".Split(',');
        private readonly string[] connectingNormCons = "b,d,g,k,p,t,dh".Split(',');
        private readonly Dictionary<string, string> dictionaryReplacements = ("aa=aa;ae=ae;ah=ah;ao=ao;aw=aw;ah0=ax;ay=ay;" +
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
        private readonly Dictionary<string, string> missingVphonemes = "ax=ah,aa=ah,ae=ah,iy=ih,uh=uw".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingVPhonemes = false;

        // For banks with missing custom consonants
        private readonly Dictionary<string, string> missingCphonemes = "by=b,dy=d,fy=f,gy=g,hy=hh,jy=jh,ky=k,ly=l,my=m,py=p,ry=r,sy=s,ty=t,vy=v,zy=z,bw=b,chw=ch,dw=d,fw=f,gw=g,hw=hh,jw=jh,kw=k,lw=l,mw=m,nw=n,pw=p,rw=r,sw=s,tw=t,vw=v,zw=w,ts=t".Split(',')
                .Select(entry => entry.Split('='))
                .Where(parts => parts.Length == 2)
                .Where(parts => parts[0] != parts[1])
                .ToDictionary(parts => parts[0], parts => parts[1]);
        private bool isMissingCPhonemes = false;

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
            };

        protected override IG2p LoadBaseDictionary() {
            var g2ps = new List<IG2p>();

            // Load dictionary from plugin folder.
            string path = Path.Combine(PluginDir, "arpasing.yaml");
            if (!File.Exists(path)) {
                Directory.CreateDirectory(PluginDir);
                File.WriteAllBytes(path, Data.Resources.arpasing_template);
            }
            g2ps.Add(G2pDictionary.NewBuilder().Load(File.ReadAllText(path)).Build());

            // Load dictionary from singer folder.
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

            // STARTING V
            if (syllable.IsStartingV) {
                // TRIES - V, -V THEN DEFAULTS TO V
                basePhoneme = CheckAliasFormatting(v, "cv", syllable.vowelTone, "");
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
                        basePhoneme = crv;
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
                                    basePhoneme = v;
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
            else if (syllable.IsVCVWithOneConsonant) {
                var vcv = $"{prevV} {cc[0]} {v}";
                var vcnv = $"{prevV} {cc[0]}{v}";
                if (HasOto(vcv, syllable.vowelTone) && !HasOto(vcnv, syllable.vowelTone)) {
                    basePhoneme = vcv;
                } else if (!HasOto(vcv, syllable.vowelTone) && HasOto(vcnv, syllable.vowelTone)) {
                    basePhoneme = vcnv;
                } else {
                    var cv = $"{cc[0]} {v}";
                    basePhoneme = cv;
                }
            } else {
                // IS VCV WITH MORE THAN ONE CONSONANT
                basePhoneme = $"{cc.Last()} {v}";

                var max = cc.Length;
                var min = 0;
            }
            // C V, THEN CV
            if (syllable.IsStartingCVWithOneConsonant) {
                var crv = $"{cc[0]} {v}";
                var cnv = $"{cc[0]}{v}";
                if (HasOto(crv, syllable.vowelTone) || HasOto(ValidateAlias(crv), syllable.vowelTone)) {
                    basePhoneme = crv;
                } else if (!HasOto(crv, syllable.vowelTone) && HasOto(cnv, syllable.vowelTone)) {
                    basePhoneme = cnv;
                } else if (HasOto(crv, syllable.vowelTone) && HasOto(cnv, syllable.vowelTone)) {
                    basePhoneme = crv;
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
            //arpasing 0.1.0 exclusive fallbacks

            //CV (bloat ik, I have to simplify this soon)
            Dictionary<string, List<string>> CVReplacements = new Dictionary<string, List<string>> {

            { "dr ax", new List<string> { "jh ah" } },
            { "tr ax", new List<string> { "ch ah" } },
            { "zh ax", new List<string> { "jh ah" } },
            { "dx aa", new List<string> { "d aa" } },
            { "dr aa", new List<string> { "jh aa" } },
            { "tr aa", new List<string> { "ch aa" } },
            { "zh aa", new List<string> { "jh aa" } },
            { "zh ae", new List<string> { "jh aa" } },
            { "ng ae", new List<string> { "ng aa" } },
            { "dx ah", new List<string> { "d ah" } },
            { "zh ah", new List<string> { "jh ah" } },
            { "ch ao", new List<string> { "sh ow" } },
            { "dh ao", new List<string> { "dh ow" } },
            { "dx ao", new List<string> { "d ao" } },
            { "jh ao", new List<string> { "jh oy" } },
            { "ao -", new List<string> { "aa -" } },
            { "ng ao", new List<string> { "ng ow" } },
            { "sh ao", new List<string> { "sh ow" } },
            { "v ao", new List<string> { "b ao" } },
            { "z ao", new List<string> { "s ao" } },
            { "zh ao", new List<string> { "z aa" } },
            { "ch aw", new List<string> { "ch ah" } },
            { "g aw", new List<string> { "g ah" } },
            { "jh aw", new List<string> { "jh ah" } },
            { "k aw", new List<string> { "k ah" } },
            { "ng aw", new List<string> { "ng ah" } },
            { "p aw", new List<string> { "p ah" } },
            { "s aw", new List<string> { "s ah" } },
            { "sh aw", new List<string> { "sh ah" } },
            { "v aw", new List<string> { "v ah" } },
            { "y aw", new List<string> { "y ah" } },
            { "w aw", new List<string> { "w ah" } },
            { "z aw", new List<string> { "z ah" } },
            { "zh aw", new List<string> { "jh ah" } },
            { "ch ay", new List<string> { "ch ah" } },
            { "dh ay", new List<string> { "dh ah" } },
            { "l ay", new List<string> { "l ah" } },
            { "ng ay", new List<string> { "ng ah" } },
            { "th ay", new List<string> { "th ah" } },
            { "y ay", new List<string> { "y ah" } },
            { "zh ay", new List<string> { "jh ay" } },
            { "jh eh", new List<string> { "jh ae" } },
            { "ng eh", new List<string> { "n eh" } },
            { "p eh", new List<string> { "p ae" } },
            { "th eh", new List<string> { "th ae" } },
            { "dx ae", new List<string> { "d ae" } },
            { "z eh", new List<string> { "s eh" } },
            { "zh eh", new List<string> { "jh ae" } },
            { "jh er", new List<string> { "z er" } },
            { "ng er", new List<string> { "n er" } },
            { "r er", new List<string> { "er" } },
            { "th er", new List<string> { "th r" } },
            { "zh er", new List<string> { "z er" } },
            { "jh ey", new List<string> { "jh ae" } },
            { "ng ey", new List<string> { "n ey" } },
            { "th ey", new List<string> { "th ae" } },
            { "zh ey", new List<string> { "jh ae" } },
            { "ch ow", new List<string> { "sh ow" } },
            { "jh ow", new List<string> { "sh ow" } },
            { "v ow", new List<string> { "b ow" } },
            { "th ow", new List<string> { "s ow" } },
            { "w ow", new List<string> { "w ao" } },
            { "z ow", new List<string> { "s ow" } },
            { "zh ow", new List<string> { "jh aa" } },
            { "dx oy", new List<string> { "d ow" } },
            { "d oy", new List<string> { "d ow" } },
            { "ch oy", new List<string> { "sh ow" } },
            { "dh oy", new List<string> { "dh ow" } },
            { "f oy", new List<string> { "f ow" } },
            { "hh oy", new List<string> { "hh ow" } },
            { "k oy", new List<string> { "k ow" } },
            { "l oy", new List<string> { "l ow" } },
            { "n oy", new List<string> { "n ow" } },
            { "ng oy", new List<string> { "ng ow" } },
            { "p oy", new List<string> { "p ow" } },
            { "q oy", new List<string> { "q ow" } },
            { "r oy", new List<string> { "r ow" } },
            { "s oy", new List<string> { "s ow" } },
            { "sh oy", new List<string> { "sh ow" } },
            { "t oy", new List<string> { "t ow" } },
            { "th oy", new List<string> { "th aa" } },
            { "v oy", new List<string> { "b oy" } },
            { "w oy", new List<string> { "w ao" } },
            { "z oy", new List<string> { "z aa" } },
            { "zh oy", new List<string> { "jh oy" } },
            { "ch uh", new List<string> { "sh uh" } },
            { "dh uh", new List<string> { "d uw" } },
            { "jh uh", new List<string> { "jh ah" } },
            { "ng uh", new List<string> { "n uw" } },
            { "th uh", new List<string> { "f uw" } },
            { "v uh", new List<string> { "b uh" } },
            { "z uh", new List<string> { "s uw" } },
            { "zh uh", new List<string> { "jh ah" } },
            { "ch uw", new List<string> { "sh uw" } },
            { "dh uw", new List<string> { "d uw" } },
            { "g uw", new List<string> { "k uw" } },
            { "jh uw", new List<string> { "sh uw" } },
            { "ng uw", new List<string> { "n uw" } },
            { "th uw", new List<string> { "f uw" } },
            { "v uw", new List<string> { "b uw" } },
            { "z uw", new List<string> { "s uw" } },
            { "zh uw", new List<string> { "sh uw" } },
            { "q ay", new List<string> { "q ah" } },
            { " oy", new List<string> { " ow" } },
            { "oy", new List<string> { "ow" } },

            };
            foreach (var vowel in CVReplacements) {

                var sourceVowel = vowel.Key;
                var replacementOptions = vowel.Value;

                foreach (var replacement in replacementOptions) {
                    if (alias.Contains(sourceVowel)) {
                        alias = alias.Replace(sourceVowel, replacement);
                        break; // Once a suitable replacement is found, exit the loop

                    }
                }
            }

            // Validate alias depending on method
            if (isMissingVPhonemes || isMissingCPhonemes) {
                foreach (var syllable in missingVphonemes.Concat(missingCphonemes)) {
                    alias = alias.Replace(syllable.Key, syllable.Value);
                }
            }

            if (CVReplacements.ContainsKey(alias)) {
                alias = CVReplacements[alias][0];
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
            foreach (var V in new[] { " oy" }) {
                alias = alias.Replace(" oy", " ow");
            }
            //CV (dx, dr, tr, zh)
            if (alias == "dx aa" || alias == "dx ae" || alias == "dx ah" || alias == "dx ao" || alias == "dx aw" || alias == "dx aa"
                || alias == "dx ay" || alias == "dx eh" || alias == "dx er" || alias == "dx ey" || alias == "dx ih" || alias == "dx iy"
                || alias == "dx ow" || alias == "dx oy" || alias == "dx uh" || alias == "dx uw" || alias == "- dx" || alias == "dx -") {
                return alias.Replace("dx", "r");
            }

            if (alias == "dr aa" || alias == "dr ae" || alias == "dr ah" || alias == "dr ao" || alias == "dr aw" || alias == "dr aa"
                || alias == "dr ay" || alias == "dr eh" || alias == "dr er" || alias == "dr ey" || alias == "dr ih" || alias == "dr iy"
                || alias == "dr ow" || alias == "dr oy" || alias == "dr uh" || alias == "dr uw" || alias == "- dr" || alias == "dr -") {
                return alias.Replace("dr", "jh");
            }
            if (alias == "tr aa" || alias == "tr ae" || alias == "tr ah" || alias == "tr ao" || alias == "tr aw" || alias == "tr aa"
                || alias == "tr ay" || alias == "tr eh" || alias == "tr er" || alias == "tr ey" || alias == "tr ih" || alias == "tr iy"
                || alias == "tr ow" || alias == "tr oy" || alias == "tr uh" || alias == "tr uw") {
                return alias.Replace("tr", "ch");
            }
            if (alias == "zh aa" || alias == "zh ae" || alias == "zh ah" || alias == "zh ao" || alias == "zh aw" || alias == "zh aa"
                || alias == "zh ay" || alias == "zh eh" || alias == "zh er" || alias == "zh ey" || alias == "zh ih" || alias == "zh iy"
                || alias == "zh ow" || alias == "zh oy" || alias == "zh uh" || alias == "zh uw" || alias == "- zh" || alias == "zh -") {
                return alias.Replace("zh", "jh");
            }
            //VC (diphthongs)
            if (alias == "aw b" || alias == "aw d" || alias == "aw dh" || alias == "aw f" || alias == "aw g" || alias == "aw hh"
                || alias == "aw k" || alias == "aw l" || alias == "aw m" || alias == "aw n" || alias == "aw p"
                || alias == "aw r" || alias == "aw s" || alias == "aw sh" || alias == "aw t" || alias == "aw th" || alias == "aw v"
                || alias == "aw w" || alias == "aw y" || alias == "aw z") {
                return alias.Replace("aw", "uw");
            }
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
            if (alias == "ay b" || alias == "ay ch" || alias == "ay d" || alias == "ay dh"
                || alias == "ay f" || alias == "ay g" || alias == "ay hh" || alias == "ay jh" || alias == "ay k" || alias == "ay l"
                || alias == "ay m" || alias == "ay p" || alias == "ay r"
                || alias == "ay s" || alias == "ay sh" || alias == "ay t" || alias == "ay th" || alias == "ay tr" || alias == "ay v"
                || alias == "ay w" || alias == "ay y" || alias == "ay z") {
                return alias.Replace("ay", "iy");
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
            if (alias == "ey b" || alias == "ey ch" || alias == "ey d" || alias == "ey dh"
                || alias == "ey f" || alias == "ey g" || alias == "ey hh" || alias == "ey jh" || alias == "ey k" || alias == "ey l"
                || alias == "ey m" || alias == "ey n" || alias == "ey p" || alias == "ey r"
                || alias == "ey s" || alias == "ey sh" || alias == "ey t" || alias == "ey th" || alias == "ey v"
                || alias == "ey w" || alias == "ey y" || alias == "ey z") {
                return alias.Replace("ey", "iy");
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
            if (alias == "ow b" || alias == "ow d" || alias == "ow dh"
                || alias == "ow f" || alias == "ow g" || alias == "ow hh" || alias == "ow k" || alias == "ow l"
                || alias == "ow m" || alias == "ow n" || alias == "ow p" || alias == "ow r"
                || alias == "ow s" || alias == "ow sh" || alias == "ow t" || alias == "ow th" || alias == "ow v"
                || alias == "ow w" || alias == "ow y" || alias == "ow z") {
                return alias.Replace("ow", "uw");
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
            if (alias == "oy b" || alias == "oy ch" || alias == "oy d" || alias == "oy dh"
                || alias == "oy g" || alias == "oy hh" || alias == "oy jh" || alias == "oy k" || alias == "oy l"
                || alias == "oy m" || alias == "oy n" || alias == "oy p" || alias == "oy r"
                || alias == "oy s" || alias == "oy sh" || alias == "oy t" || alias == "oy th" || alias == "oy v"
                || alias == "oy w" || alias == "oy y" || alias == "oy z") {
                return alias.Replace("oy", "iy");
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
            //VC (aa)
            if (alias == "aa b" || alias == "aa d" || alias == "aa dh" || alias == "aa dr" || alias == "aa dx"
                || alias == "aa f" || alias == "aa g" || alias == "aa hh" || alias == "aa jh" || alias == "aa k" || alias == "aa l"
                || alias == "aa m" || alias == "aa n" || alias == "aa ng" || alias == "aa p" || alias == "aa q" || alias == "aa r"
                || alias == "aa s" || alias == "aa sh" || alias == "aa t" || alias == "aa th" || alias == "aa tr" || alias == "aa v"
                || alias == "aa w" || alias == "aa y" || alias == "aa z" || alias == "aa zh") {
                return alias.Replace("aa", "ah");
            }
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
            //VC (ae)
            if (alias == "ae b" || alias == "ae d" || alias == "ae ch" || alias == "ae dh" || alias == "ae dr" || alias == "ae dx"
                || alias == "ae f" || alias == "ae g" || alias == "ae hh" || alias == "ae jh" || alias == "ae k" || alias == "ae l"
                || alias == "ae m" || alias == "ae n" || alias == "ae ng" || alias == "ae p" || alias == "ae q" || alias == "ae r"
                || alias == "ae s" || alias == "ae sh" || alias == "ae t" || alias == "ae th" || alias == "ae tr" || alias == "ae v"
                || alias == "ae w" || alias == "ae y" || alias == "ae z" || alias == "ae zh") {
                return alias.Replace("ae", "ah");
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
            if (alias == "ao b" || alias == "ao d" || alias == "ao ch" || alias == "ao dh" || alias == "ao dr" || alias == "ao dx"
                || alias == "ao f" || alias == "ao g" || alias == "ao hh" || alias == "ao jh" || alias == "ao k" || alias == "ao l"
                || alias == "ao m" || alias == "ao n" || alias == "ao ng" || alias == "ao p" || alias == "ao q" || alias == "ao r"
                || alias == "ao s" || alias == "ao sh" || alias == "ao t" || alias == "ao th" || alias == "ao tr" || alias == "ao v"
                || alias == "ao w" || alias == "ao y" || alias == "ao z" || alias == "ao zh") {
                return alias.Replace("ao", "ah");
            }
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
            if (alias == "ax b" || alias == "ax d" || alias == "ax ch" || alias == "ax dh" || alias == "ax dr" || alias == "ax dx"
                || alias == "ax f" || alias == "ax g" || alias == "ax hh" || alias == "ax jh" || alias == "ax k" || alias == "ax l"
                || alias == "ax m" || alias == "ax n" || alias == "ax ng" || alias == "ax p" || alias == "ax q" || alias == "ax r"
                || alias == "ax s" || alias == "ax sh" || alias == "ax t" || alias == "ax th" || alias == "ax tr" || alias == "ax v"
                || alias == "ax w" || alias == "ax y" || alias == "ax z" || alias == "ax zh") {
                return alias.Replace("ax", "ah");
            }
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
            if (alias == "eh d" || alias == "eh dh" || alias == "eh dr" || alias == "eh dx"
                || alias == "eh f" || alias == "eh g" || alias == "eh hh" || alias == "eh jh" || alias == "eh k" || alias == "eh l"
                || alias == "eh m" || alias == "eh n" || alias == "eh ng" || alias == "eh p" || alias == "eh q" || alias == "eh r"
                || alias == "eh s" || alias == "eh sh" || alias == "eh t" || alias == "eh th" || alias == "eh tr" || alias == "eh v"
                || alias == "eh w" || alias == "eh z") {
                return alias.Replace("eh", "ah");
            }
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
            if (alias == "uh b" || alias == "uh d" || alias == "uh dh" || alias == "uh dr" || alias == "uh dx"
                || alias == "uh f" || alias == "uh g" || alias == "uh hh" || alias == "uh jh" || alias == "uh k" || alias == "uh l"
                || alias == "uh m" || alias == "uh n" || alias == "uh ng" || alias == "uh p" || alias == "uh q" || alias == "uh r"
                || alias == "uh s" || alias == "uh sh" || alias == "uh t" || alias == "uh th" || alias == "uh tr" || alias == "uh v"
                || alias == "uh w" || alias == "uh y" || alias == "uh z" || alias == "uh zh") {
                return alias.Replace("uh", "uw");
            }
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
            if (alias == "b b" || alias == "b d" || alias == "b dh"
                || alias == "b f" || alias == "b g" || alias == "b hh" || alias == "b jh" || alias == "b k"
                || alias == "b l" || alias == "b m" || alias == "b n" || alias == "b p" || alias == "b q"
                || alias == "b r" || alias == "b s" || alias == "b sh" || alias == "b t"
                || alias == "b w" || alias == "b y" || alias == "b z") {
                return alias.Replace("b", "d");
            }
            //CC (b specific)
            if (alias == "b ch") {
                return alias.Replace("b ch", "t ch");
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
            if (alias == "- b") {
                return alias.Replace("b", "d");
            }
            if (alias == "b -") {
                return alias.Replace("b", "d");
            }
            //CC (ch specific)
            if (alias == "ch b") {
                return alias.Replace("ch b", "ch -");
            }
            if (alias == "ch d") {
                return alias.Replace("ch d", "ch -");
            }
            if (alias == "ch ch") {
                return alias.Replace("ch ch", "ch -");
            }
            if (alias == "ch dh") {
                return alias.Replace("ch dh", "ch -");
            }
            if (alias == "ch dr") {
                return alias.Replace("ch dr", "ch -");
            }
            if (alias == "ch dx") {
                return alias.Replace("ch dx", "ch -");
            }
            if (alias == "ch f") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch g") {
                return alias.Replace("ch g", "ch -");
            }
            if (alias == "ch hh") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch jh") {
                return alias.Replace("ch jh", "ch -");
            }
            if (alias == "ch k") {
                return alias.Replace("ch k", "ch -");
            }
            if (alias == "ch l") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch m") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch n") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch ng") {
                return alias.Replace("ch ng", "s n");
            }
            if (alias == "ch p") {
                return alias.Replace("ch p", "ch -");
            }
            if (alias == "ch q") {
                return alias.Replace("ch q", "ch -");
            }
            if (alias == "ch r") {
                return alias.Replace("ch r", "ch ah");
            }
            if (alias == "ch s") {
                return alias.Replace("ch", "s");
            }
            if (alias == "ch sh") {
                return alias.Replace("ch sh", "s s");
            }
            if (alias == "ch t") {
                return alias.Replace("ch t", "ch -");
            }
            if (alias == "ch th") {
                return alias.Replace("ch th", "ch -");
            }
            if (alias == "ch tr") {
                return alias.Replace("ch tr", "ch -");
            }
            if (alias == "ch v") {
                return alias.Replace("ch v", "ch -");
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
            if (alias == "ch zh") {
                return alias.Replace("ch zh", "ch -");
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
            if (alias == "dh b" || alias == "dh d" || alias == "dh dh"
                || alias == "dh f" || alias == "dh g" || alias == "dh hh" || alias == "dh jh" || alias == "dh k"
                || alias == "dh l" || alias == "dh m" || alias == "dh n" || alias == "dh p" || alias == "dh q"
                || alias == "dh r" || alias == "dh s" || alias == "dh sh" || alias == "dh t" || alias == "dh th"
                || alias == "dh w" || alias == "dh y" || alias == "dh z") {
                return alias.Replace("dh", "d");
            }
            //CC (dh specific)
            if (alias == "dh ch") {
                return alias.Replace("dh ch", "t ch");
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
            if (alias == "dx b" || alias == "dx d" || alias == "dx dh"
                || alias == "dx f" || alias == "dx g" || alias == "dx hh" || alias == "dx jh" || alias == "dx k"
                || alias == "dx l" || alias == "dx m" || alias == "dx n" || alias == "dx p" || alias == "dx q"
                || alias == "dx r" || alias == "dx s" || alias == "dx sh" || alias == "dx t" || alias == "dx th"
                || alias == "dx w" || alias == "dx y" || alias == "dx z") {
                return alias.Replace("dx", "d");
            }
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
            if (alias == "f b" || alias == "f d" || alias == "f ch" || alias == "f dh"
                || alias == "f f" || alias == "f g" || alias == "f hh" || alias == "f jh" || alias == "f k"
                || alias == "f l" || alias == "f m" || alias == "f n" || alias == "f p"
                || alias == "f r" || alias == "f s" || alias == "f t"
                || alias == "f w") {
                return alias.Replace("f", "s");
            }
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
            if (alias == "g b" || alias == "g d" || alias == "g dh"
                || alias == "g f" || alias == "g g" || alias == "g hh" || alias == "g jh" || alias == "g k"
                || alias == "g l" || alias == "g m" || alias == "g n" || alias == "g p" || alias == "g q"
                || alias == "g r" || alias == "g s" || alias == "g sh" || alias == "g t" || alias == "g th"
                || alias == "g w" || alias == "g y" || alias == "g z") {
                return alias.Replace("g", "d");
            }
            //CC (g specific)
            if (alias == "g ch") {
                return alias.Replace("g ch", "t ch");
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
            if (alias == "hh b" || alias == "hh d" || alias == "hh ch" || alias == "hh dh"
                || alias == "hh f" || alias == "hh g" || alias == "hh hh" || alias == "hh jh" || alias == "hh k"
                || alias == "hh l" || alias == "hh m" || alias == "hh n" || alias == "hh p"
                || alias == "hh r" || alias == "hh s" || alias == "hh t") {
                return alias.Replace("hh", "s");
            }
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
            if (alias == "hh g") {
                return alias.Replace("hh", "s");
            }
            if (alias == "hh hh") {
                return alias.Replace("hh", "s");
            }
            if (alias == "hh jh") {
                return alias.Replace("hh", "s");
            }
            if (alias == "hh k") {
                return alias.Replace("hh", "s");
            }
            if (alias == "hh l") {
                return alias.Replace("hh", "f");
            }
            if (alias == "hh m") {
                return alias.Replace("hh", "s");
            }
            if (alias == "hh n") {
                return alias.Replace("hh", "s");
            }
            if (alias == "hh ng") {
                return alias.Replace("hh ng", "s n");
            }
            if (alias == "hh p") {
                return alias.Replace("hh", "s");
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
                return alias.Replace("hh", "f");
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
                return alias.Replace("hh", "f");
            }
            //CC (jh specific)
            if (alias == "jh b") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh d") {
                return alias.Replace("jh d", "jh -");
            }
            if (alias == "jh ch") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh dh") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh dr") {
                return alias.Replace("jh dr", "jh -");
            }
            if (alias == "jh dx") {
                return alias.Replace("jh dx", "jh -");
            }
            if (alias == "jh f") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh g") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh hh") {
                return alias.Replace("jh", "s");
            }
            if (alias == "jh jh") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh k") {
                return alias.Replace("jh", "jh -");
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
            if (alias == "jh p") {
                return alias.Replace("jh", "jh -");
            }
            if (alias == "jh q") {
                return alias.Replace("jh p", "jh -");
            }
            if (alias == "jh r") {
                return alias.Replace("jh r", "jh ah");
            }
            if (alias == "jh s") {
                return alias.Replace("jh", "f");
            }
            if (alias == "jh sh") {
                return alias.Replace("jh sh", "jh -");
            }
            if (alias == "jh t") {
                return alias.Replace("jh t", "f");
            }
            if (alias == "jh th") {
                return alias.Replace("jh th", "jh -");
            }
            if (alias == "jh tr") {
                return alias.Replace("jh tr", "jh -");
            }
            if (alias == "jh v") {
                return alias.Replace("jh v", "jh -");
            }
            if (alias == "jh w") {
                return alias.Replace("jh", "f");
            }
            if (alias == "jh y") {
                return alias.Replace("y", "iy");
            }
            if (alias == "jh z") {
                return alias.Replace("jh z", "s s");
            }
            if (alias == "jh zh") {
                return alias.Replace("jh zh", "jh -");
            }
            //CC (k)
            if (alias == "k b" || alias == "k d" || alias == "k dh"
            || alias == "k f" || alias == "k g" || alias == "k hh" || alias == "k jh" || alias == "k k"
            || alias == "k l" || alias == "k m" || alias == "k n" || alias == "k ng" || alias == "k p" || alias == "k q"
            || alias == "k r" || alias == "k s" || alias == "k sh" || alias == "k t" || alias == "k th"
            || alias == "k w" || alias == "k y") {
                return alias.Replace("k", "t");
            }
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
                return alias.Replace("dx", "d");
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
                return alias.Replace("l th", "er th");
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
            if (alias == "ng b" || alias == "ng d" || alias == "ng dh" || alias == "ng dr" || alias == "ng dx"
                || alias == "ng f" || alias == "ng g" || alias == "ng hh" || alias == "ng jh" || alias == "ng k"
                || alias == "ng l" || alias == "ng m" || alias == "ng n" || alias == "ng ng" || alias == "ng p" || alias == "ng q"
                || alias == "ng r" || alias == "ng s" || alias == "ng sh" || alias == "ng t" || alias == "ng th" || alias == "ng tr"
                 || alias == "ng v" || alias == "ng w" || alias == "ng y" || alias == "ng z" || alias == "ng zh") {
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
            if (alias == "p b" || alias == "p d" || alias == "p dh"
            || alias == "p f" || alias == "p g" || alias == "p hh" || alias == "p jh" || alias == "p k"
            || alias == "p l" || alias == "p m" || alias == "p n" || alias == "p ng" || alias == "p p" || alias == "p q"
            || alias == "p r" || alias == "p s" || alias == "p sh" || alias == "p t" || alias == "p th"
            || alias == "p w" || alias == "p y") {
                return alias.Replace("p", "t");
            }
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
            if (alias == "q b" || alias == "q d" || alias == "q dh" || alias == "q dr" || alias == "q dx"
            || alias == "q f" || alias == "q g" || alias == "q hh" || alias == "q jh" || alias == "q k"
            || alias == "q l" || alias == "q m" || alias == "q n" || alias == "q ng" || alias == "q p" || alias == "q q"
            || alias == "q r" || alias == "q s" || alias == "q sh" || alias == "q t" || alias == "q th" || alias == "q tr"
            || alias == "q v" || alias == "q w" || alias == "q y" || alias == "q z" || alias == "q zh") {
                return alias.Replace("q", "-");
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
            if (alias == "s tr") {
                return alias.Replace("s tr", "s -");
            }
            if (alias == "s zh") {
                return alias.Replace("zh", "s");
            }
            //CC (sh specific)
            if (alias == "sh b") {
                return alias.Replace("sh b", "sh -");
            }
            if (alias == "sh d") {
                return alias.Replace("sh d", "sh -");
            }
            if (alias == "sh ch") {
                return alias.Replace("sh ch", "sh -");
            }
            if (alias == "sh dh") {
                return alias.Replace("sh dh", "sh -");
            }
            if (alias == "sh dr") {
                return alias.Replace("sh dr", "sh -");
            }
            if (alias == "sh dx") {
                return alias.Replace("sh dx", "sh -");
            }
            if (alias == "sh f") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh g") {
                return alias.Replace("sh g", "sh -");
            }
            if (alias == "sh hh") {
                return alias.Replace("sh", "s");
            }
            if (alias == "sh jh") {
                return alias.Replace("sh jh", "sh -");
            }
            if (alias == "sh k") {
                return alias.Replace("sh k", "sh -");
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
            if (alias == "sh p") {
                return alias.Replace("sh p", "sh -");
            }
            if (alias == "sh q") {
                return alias.Replace("sh q", "sh -");
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
            if (alias == "sh t") {
                return alias.Replace("sh t", "sh -");
            }
            if (alias == "sh th") {
                return alias.Replace("sh th", "th");
            }
            if (alias == "sh tr") {
                return alias.Replace("sh tr", "sh -");
            }
            if (alias == "sh v") {
                return alias.Replace("sh v", "sh -");
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
            if (alias == "sh zh") {
                return alias.Replace("sh zh", "sh -");
            }
            //CC (t specific)
            if (alias == "t dr") {
                return alias.Replace("t dr", "t -");
            }
            if (alias == "t dx") {
                return alias.Replace("t dx", "t -");
            }
            if (alias == "t tr") {
                return alias.Replace("t tr", "t -");
            }
            if (alias == "t v") {
                return alias.Replace("t v", "t -");
            }
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
            if (alias == "th b" || alias == "th d" || alias == "th ch" || alias == "th dh"
            || alias == "th f" || alias == "th g" || alias == "th hh" || alias == "th jh" || alias == "th k"
            || alias == "th l" || alias == "th m" || alias == "th n" || alias == "th p"
            || alias == "th r" || alias == "th s" || alias == "th t"
            || alias == "th w") {
                return alias.Replace("th", "s");
            }
            //CC (th specific)
            if (alias == "th dr") {
                return alias.Replace("th dr", "s jh");
            }
            if (alias == "th dx") {
                return alias.Replace("th dx", "th -");
            }
            if (alias == "th ng") {
                return alias.Replace("th ng", "th");
            }
            if (alias == "th q") {
                return alias.Replace("th q", "th -");
            }
            if (alias == "th sh") {
                return alias.Replace("th sh", "th");
            }
            if (alias == "th th") {
                return alias.Replace("th th", "th");
            }
            if (alias == "th tr") {
                return alias.Replace("th tr", "th -");
            }
            if (alias == "th v") {
                return alias.Replace("th v", "th");
            }
            if (alias == "th z") {
                return alias.Replace("th z", "th");
            }
            if (alias == "th zh") {
                return alias.Replace("zh", "s");
            }
            //CC (v specific)
            if (alias == "v b") {
                return alias.Replace("v b", "v -");
            }
            if (alias == "v d") {
                return alias.Replace("v d", "v -");
            }
            if (alias == "v ch") {
                return alias.Replace("v ch", "v -");
            }
            if (alias == "v dh") {
                return alias.Replace("v dh", "v -");
            }
            if (alias == "v dr") {
                return alias.Replace("v dr", "v -");
            }
            if (alias == "v dx") {
                return alias.Replace("v dx", "v -");
            }
            if (alias == "v f") {
                return alias.Replace("v", "s");
            }
            if (alias == "v g") {
                return alias.Replace("v g", "v -");
            }
            if (alias == "v hh") {
                return alias.Replace("v", "s");
            }
            if (alias == "v jh") {
                return alias.Replace("v jh", "v -");
            }
            if (alias == "v k") {
                return alias.Replace("v k", "v -");
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
            if (alias == "v p") {
                return alias.Replace("v p", "v -");
            }
            if (alias == "v q") {
                return alias.Replace("v p", "v -");
            }
            if (alias == "v r") {
                return alias.Replace("v", "s");
            }
            if (alias == "v s") {
                return alias.Replace("v", "s");
            }
            if (alias == "v sh") {
                return alias.Replace("v sh", "s s");
            }
            if (alias == "v t") {
                return alias.Replace("v t", "v -");
            }
            if (alias == "v th") {
                return alias.Replace("v th", "v -");
            }
            if (alias == "v tr") {
                return alias.Replace("v tr", "v -");
            }
            if (alias == "v v") {
                return alias.Replace("v v", "v -");
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
            if (alias == "v zh") {
                return alias.Replace("v zh", "v -");
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
            if (alias == "zh b" || alias == "zh d" || alias == "zh dh"
                || alias == "zh f" || alias == "zh g" || alias == "zh hh" || alias == "zh jh" || alias == "zh k"
                || alias == "zh l" || alias == "zh m" || alias == "zh n" || alias == "zh p"
                || alias == "zh r" || alias == "zh s" || alias == "zh sh" || alias == "zh t" || alias == "zh th" || alias == "zh tr"
                 || alias == "zh v" || alias == "zh w" || alias == "zh y") {
                return alias.Replace("zh", "z");
            }
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

            } else {
                return base.ValidateAlias(alias);
            }
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
                if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains("ng -")) {
                    return base.GetTransitionBasicLengthMs() * 2.3;
                }
            }

            foreach (var c in normalConsonants) {
                foreach (var v in normalConsonants.Except(GlideVCCons)) {
                    foreach (var b in normalConsonants.Except(NormVCCons)) {
                        if (alias.Contains(c) && !alias.StartsWith(c) &&
                            !alias.Contains("dx") && !alias.Contains("ng")) {
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
                    if (alias.Contains($"{v} {c}")) {
                        return base.GetTransitionBasicLengthMs() * 2.5;

                    }
                }
            }

            foreach (var c in connectingGlides) {
                foreach (var v in vowels.Where(v => excludedVowels.Contains(v))) {
                    if (alias.Contains($"{v} {c}")) {
                        return base.GetTransitionBasicLengthMs() * 0.6;

                    }
                }
            }

            foreach (var c in semilongConsonants) {
                foreach (var v in semilongConsonants.Except(excludedEndings)) {
                    if (alias.Contains(c) && !alias.StartsWith(c) && !alias.Contains($"{c} -")) {
                        return base.GetTransitionBasicLengthMs() * 2.0;
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
        private string CheckAliasFormatting(string alias, string type, int tone, string prevV) {

            var checkAliasFormat = "";
            string[] aliasFormats = new string[] { "- ", "-", "", " -", "-", "", prevV, prevV + " ", "_", "", prevV, " " + prevV };
            var startingI = 0;
            var endingI = aliasFormats.Length;


            if (type == "ending-") {
                startingI = 3;
                endingI = startingI + 1;
            }

            if (type == "ending -") {
                startingI = 3;
                endingI = startingI + 2;
            }

            if (type == "rcv") {
                startingI = 0;
                endingI = startingI + 1;
            }

            if (type == "crv") {
                startingI = 1;
                endingI = startingI + 1;
            }

            if (type == "vv") {
                startingI = 6;
                endingI = startingI + 3;
            }

            if (type == "cc") {
                startingI = 6;
                endingI = startingI + 1;
            }

            if (type == "cv") {
                startingI = 0;
                endingI = startingI + 2;
            }


            for (int i = startingI; i <= endingI; i++) {
                if (type.Contains("ending")) {
                    checkAliasFormat = alias + aliasFormats[i];
                } else checkAliasFormat = aliasFormats[i] + alias;

                if (HasOto(checkAliasFormat, tone)) {
                    alias = checkAliasFormat;
                    return alias;
                }
            }

            return "no alias";
        }
    }
}
