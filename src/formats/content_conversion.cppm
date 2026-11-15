export module openciv.formats.content_conversion;

import std;
export import openciv.formats.content_pack;
export import openciv.formats.classic_text_reader;

export namespace openciv::formats::content {
struct ContentConversion {
    std::string component; // Exact UTF-8 native component bytes, ready for validation/publication.
    jt::Json receipt;
};
struct RuleConversion {
    std::string rules, compatibility;
    jt::Json receipt;
};
RuleConversion convert_rules(const Source& source, const classic::TextCase& comparison);
// M1-02 component conversion. Explicit owned classic source and resolved native
// rule selection; no filesystem access, mutation, fallback mounts or publication.
ContentConversion convert_city_preferences(const Source& source, const ResolvedPack& selected,
                                           const classic::TextCase& comparison);
ContentConversion convert_city_names(const Source& source, const ResolvedPack& selected,
                                     const classic::TextCase& comparison);
} // namespace openciv::formats::content
