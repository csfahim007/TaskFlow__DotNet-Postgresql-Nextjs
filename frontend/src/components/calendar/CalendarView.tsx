"use client";

import { useState } from "react";
import { Task } from "@/services/tasks.service";

interface CalendarViewProps {
  tasks: Task[];
  onTaskClick: (task: Task) => void;
  onDateClick: (date: Date) => void;
}

export function CalendarView({ tasks, onTaskClick, onDateClick }: CalendarViewProps) {
  const [currentDate, setCurrentDate] = useState(() => new Date());

  const year = currentDate.getFullYear();
  const month = currentDate.getMonth();

  // First day of month (0 = Sun, 1 = Mon...)
  const firstDayOfMonth = new Date(year, month, 1).getDay();
  // Days in current month
  const daysInCurrentMonth = new Date(year, month + 1, 0).getDate();
  // Days in previous month
  const daysInPrevMonth = new Date(year, month, 0).getDate();

  const prevMonth = () => setCurrentDate(new Date(year, month - 1, 1));
  const nextMonth = () => setCurrentDate(new Date(year, month + 1, 1));
  const goToToday = () => setCurrentDate(new Date());

  const monthNames = [
    "January", "February", "March", "April", "May", "June",
    "July", "August", "September", "October", "November", "December"
  ];
  const weekdays = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];

  // Helper to test if a date is today
  const today = new Date();
  const isToday = (d: number, m: number, y: number) =>
    today.getDate() === d && today.getMonth() === m && today.getFullYear() === y;

  // Filter tasks with dueDate matching a specific year-month-day
  const getTasksForDate = (y: number, m: number, d: number) => {
    return tasks.filter((t) => {
      if (!t.dueDate) return false;
      const taskDate = new Date(t.dueDate);
      return (
        taskDate.getFullYear() === y &&
        taskDate.getMonth() === m &&
        taskDate.getDate() === d
      );
    });
  };

  // Build grid calendar cells
  interface DayCell {
    day: number;
    month: number;
    year: number;
    isCurrentMonth: boolean;
    date: Date;
  }

  const cells: DayCell[] = [];

  // Trailing days from previous month
  for (let i = firstDayOfMonth - 1; i >= 0; i--) {
    const day = daysInPrevMonth - i;
    const m = month === 0 ? 11 : month - 1;
    const y = month === 0 ? year - 1 : year;
    cells.push({ day, month: m, year: y, isCurrentMonth: false, date: new Date(y, m, day) });
  }

  // Days of current month
  for (let day = 1; day <= daysInCurrentMonth; day++) {
    cells.push({ day, month, year, isCurrentMonth: true, date: new Date(year, month, day) });
  }

  // Next month filler to reach multiple of 7 (up to 35 or 42)
  const remaining = 42 - cells.length;
  for (let day = 1; day <= remaining; day++) {
    const m = month === 11 ? 0 : month + 1;
    const y = month === 11 ? year + 1 : year;
    cells.push({ day, month: m, year: y, isCurrentMonth: false, date: new Date(y, m, day) });
  }

  // Monthly stats
  const monthTasks = tasks.filter((t) => {
    if (!t.dueDate) return false;
    const td = new Date(t.dueDate);
    return td.getFullYear() === year && td.getMonth() === month;
  });
  const completedMonthTasks = monthTasks.filter((t) => t.status === "Done").length;
  const overdueMonthTasks = monthTasks.filter(
    (t) => t.status !== "Done" && new Date(t.dueDate!) < new Date()
  ).length;

  return (
    <div className="calendar-view-container animate-fade-in">
      {/* Calendar Toolbar */}
      <div className="calendar-toolbar">
        <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
          <h2 style={{ fontSize: 18, fontWeight: 700, color: "var(--text-primary)", margin: 0 }}>
            {monthNames[month]} {year}
          </h2>
          <div style={{ display: "flex", gap: 4 }}>
            <button className="btn btn-secondary btn-sm" onClick={prevMonth} title="Previous month">
              ‹
            </button>
            <button className="btn btn-secondary btn-sm" onClick={goToToday}>
              Today
            </button>
            <button className="btn btn-secondary btn-sm" onClick={nextMonth} title="Next month">
              ›
            </button>
          </div>
        </div>

        {/* Stats */}
        <div style={{ display: "flex", alignItems: "center", gap: 16, fontSize: 12 }}>
          <span style={{ color: "var(--text-muted)" }}>
            <strong style={{ color: "var(--text-primary)" }}>{monthTasks.length}</strong> tasks due
          </span>
          <span style={{ color: "var(--text-muted)" }}>
            <strong style={{ color: "#10b981" }}>{completedMonthTasks}</strong> completed
          </span>
          {overdueMonthTasks > 0 && (
            <span style={{ color: "#f87171", fontWeight: 600 }}>
              ⚠ {overdueMonthTasks} overdue
            </span>
          )}
        </div>
      </div>

      {/* Weekday headers */}
      <div className="calendar-weekdays">
        {weekdays.map((w) => (
          <div key={w} className="calendar-weekday">
            {w}
          </div>
        ))}
      </div>

      {/* Days Grid */}
      <div className="calendar-days-grid">
        {cells.map((cell, idx) => {
          const dayTasks = getTasksForDate(cell.year, cell.month, cell.day);
          const isTodayDate = isToday(cell.day, cell.month, cell.year);

          return (
            <div
              key={idx}
              className={`calendar-day-cell ${!cell.isCurrentMonth ? "other-month" : ""} ${
                isTodayDate ? "is-today" : ""
              }`}
              onClick={(e) => {
                if ((e.target as HTMLElement).closest(".calendar-task-item")) return;
                onDateClick(cell.date);
              }}
              title="Click to add a task for this date"
            >
              <div className="calendar-day-header">
                <span className="calendar-day-number">{cell.day}</span>
                {dayTasks.length > 0 && (
                  <span style={{ fontSize: 10, color: "var(--text-muted)", fontWeight: 600 }}>
                    {dayTasks.length} {dayTasks.length === 1 ? "task" : "tasks"}
                  </span>
                )}
              </div>

              <div className="calendar-day-tasks">
                {dayTasks.map((task) => {
                  const isDone = task.status === "Done";
                  const isOverdueTask = !isDone && new Date(task.dueDate!) < new Date();

                  return (
                    <div
                      key={task.id}
                      className={`calendar-task-item ${isDone ? "status-done" : ""} ${
                        isOverdueTask ? "is-overdue" : ""
                      } priority-${task.priority.toLowerCase()}`}
                      onClick={(e) => {
                        e.stopPropagation();
                        onTaskClick(task);
                      }}
                      title={`${task.taskNumber}: ${task.title} (${task.status})`}
                    >
                      <span style={{ opacity: 0.75, marginRight: 4, fontFamily: "monospace" }}>
                        {task.taskNumber}
                      </span>
                      {task.title}
                    </div>
                  );
                })}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}
