#ifndef CONFIG_H
#define CONFIG_H

typedef struct {
    int font_ui_size;
    int font_title_size;
    int font_mono_size;
    int motor_c_enabled;           // 1 = Ativo (C Native), 0 = Desativado (Python Fallback)
    int reverse_tethering_enabled; // 1 = Automático (USB Internet), 0 = Desativado
    int ai_learn_active;           // 0 = Estático/Determinístico (seed fixo, sem persistência), 1 = Ativo (aprende e persiste bot_learn.json)
} AppConfig;

void config_load(AppConfig* cfg);
void config_save(const AppConfig* cfg);

#endif // CONFIG_H
