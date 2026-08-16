#ifndef CONFIG_H
#define CONFIG_H

typedef struct {
    int font_ui_size;
    int font_title_size;
    int font_mono_size;
    int motor_c_enabled;           // 1 = Ativo (C Native), 0 = Desativado (Python Fallback)
    int reverse_tethering_enabled; // 1 = Automático (USB Internet), 0 = Desativado
} AppConfig;

void config_load(AppConfig* cfg);
void config_save(const AppConfig* cfg);

#endif // CONFIG_H
