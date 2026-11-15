export module openciv.platform.content_files;

import std;
export import openciv.formats.gameplay;

export extern "C++" {
    namespace openciv::io {
    class ContentDirectory {
    public:
        explicit ContentDirectory(const std::filesystem::path& mounted_root);
        formats::content::Source read(std::string_view relative,
                                      std::size_t maximum_bytes = 16 * 1024 * 1024,
                                      std::string_view required_directory = {});

    private:
        std::filesystem::path root_;
        std::vector<std::filesystem::path> backing_;
    };
    } // namespace openciv::io
}
