using Microsoft.EntityFrameworkCore;
using TaskFlow.Api.Domain;

namespace TaskFlow.Api.Data;

public static class DemoSeed
{
    public static async Task RunAsync(TaskFlowDb db, TimeProvider clock, CancellationToken ct)
    {
        if (await db.TeamMembers.AnyAsync(ct)) return;
        var members = new[]
        {
            new TeamMember { Name = "Анна Мороз", Role = "Backend developer", Initials = "АМ" },
            new TeamMember { Name = "Марк Волков", Role = "Frontend developer", Initials = "МВ" },
            new TeamMember { Name = "София Орлова", Role = "QA engineer", Initials = "СО" }
        };
        db.TeamMembers.AddRange(members);
        await db.SaveChangesAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var samples = new (string Title, string Description, WorkStatus Status, Priority Priority, int? Member, int Days)[]
        {
            ("Подключить уведомления о заявках", "Отправлять команде уведомление после создания новой заявки. Согласовать формат сообщения.", WorkStatus.New, Priority.High, members[0].Id, 2),
            ("Исправить мобильное меню", "На небольших экранах меню перекрывает кнопку отправки. Проверить ширины 390 и 768 px.", WorkStatus.InProgress, Priority.High, members[1].Id, -2),
            ("Подготовить сценарии приёмки", "Описать создание заявки, назначение исполнителя и обработку некорректных данных.", WorkStatus.New, Priority.Medium, members[2].Id, 4),
            ("Добавить фильтр по исполнителю", "Фильтр должен работать вместе с поиском и сохранять выбранный порядок сортировки.", WorkStatus.InProgress, Priority.Medium, members[0].Id, 1),
            ("Проверить экспорт отчёта", "Проверить заголовки CSV и корректное отображение кириллицы в табличном редакторе.", WorkStatus.Done, Priority.Low, members[2].Id, -1),
            ("Уточнить требования к интеграции", "У заказчика пока нет схемы данных. Собрать вопросы и согласовать границы первого этапа.", WorkStatus.New, Priority.Low, null, 6),
            ("Добавить проверку обязательных полей", "Пустое название не должно создавать запись. Ошибка должна быть понятной пользователю.", WorkStatus.Done, Priority.High, members[0].Id, -3),
            ("Согласовать макет карточки заявки", "Показать описание, срок, приоритет, исполнителя и историю изменений в одном окне.", WorkStatus.New, Priority.Medium, members[1].Id, -1),
            ("Проверить повторное сохранение", "Открыть одну заявку в двух окнах и убедиться, что устаревшие изменения не затрут новые.", WorkStatus.InProgress, Priority.Medium, members[2].Id, 3)
        };
        foreach (var (title, description, status, priority, member, days) in samples)
        {
            var item = new WorkItem { Title = title, Description = description, Status = status, Priority = priority,
                AssigneeId = member, DueDate = today.AddDays(days), CreatedAt = now.AddDays(-5), UpdatedAt = now.AddDays(-1) };
            item.History.Add(new() { Action = "Создана демонстрационная заявка", At = item.CreatedAt });
            if (status != WorkStatus.New) item.History.Add(new() { Action = $"Демонстрационный статус: {status}", At = item.UpdatedAt });
            db.WorkItems.Add(item);
        }
        await db.SaveChangesAsync(ct);
    }
}
