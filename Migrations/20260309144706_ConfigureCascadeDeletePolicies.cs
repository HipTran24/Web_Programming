using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Web_Project.Migrations
{
    /// <inheritdoc />
    public partial class ConfigureCascadeDeletePolicies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIProcesses_Contents_ContentId",
                table: "AIProcesses");

            migrationBuilder.DropForeignKey(
                name: "FK_AISystemLogs_Users_UserId",
                table: "AISystemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentModerations_Contents_ContentId",
                table: "ContentModerations");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentModerations_Users_ReviewedByUserId",
                table: "ContentModerations");

            migrationBuilder.DropForeignKey(
                name: "FK_Contents_Users_UserId",
                table: "Contents");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyUsageCounters_GuestSessions_GuestSessionId",
                table: "DailyUsageCounters");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyUsageCounters_Users_UserId",
                table: "DailyUsageCounters");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Quizzes_QuizId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_Quizzes_QuizId",
                table: "QuizAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_Users_UserId",
                table: "QuizAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Contents_ContentId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyStatistics_Users_UserId",
                table: "StudyStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_UpdatedByUserId",
                table: "SystemSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnswers_QuizAttempts_AttemptId",
                table: "UserAnswers");

            migrationBuilder.AddForeignKey(
                name: "FK_AIProcesses_Contents_ContentId",
                table: "AIProcesses",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "ContentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_AISystemLogs_Users_UserId",
                table: "AISystemLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentModerations_Contents_ContentId",
                table: "ContentModerations",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "ContentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ContentModerations_Users_ReviewedByUserId",
                table: "ContentModerations",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Contents_Users_UserId",
                table: "Contents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyUsageCounters_GuestSessions_GuestSessionId",
                table: "DailyUsageCounters",
                column: "GuestSessionId",
                principalTable: "GuestSessions",
                principalColumn: "GuestSessionId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_DailyUsageCounters_Users_UserId",
                table: "DailyUsageCounters",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Quizzes_QuizId",
                table: "Questions",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "QuizId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAttempts_Quizzes_QuizId",
                table: "QuizAttempts",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "QuizId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAttempts_Users_UserId",
                table: "QuizAttempts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Contents_ContentId",
                table: "Quizzes",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "ContentId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StudyStatistics_Users_UserId",
                table: "StudyStatistics",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_UpdatedByUserId",
                table: "SystemSettings",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnswers_QuizAttempts_AttemptId",
                table: "UserAnswers",
                column: "AttemptId",
                principalTable: "QuizAttempts",
                principalColumn: "AttemptId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AIProcesses_Contents_ContentId",
                table: "AIProcesses");

            migrationBuilder.DropForeignKey(
                name: "FK_AISystemLogs_Users_UserId",
                table: "AISystemLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentModerations_Contents_ContentId",
                table: "ContentModerations");

            migrationBuilder.DropForeignKey(
                name: "FK_ContentModerations_Users_ReviewedByUserId",
                table: "ContentModerations");

            migrationBuilder.DropForeignKey(
                name: "FK_Contents_Users_UserId",
                table: "Contents");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyUsageCounters_GuestSessions_GuestSessionId",
                table: "DailyUsageCounters");

            migrationBuilder.DropForeignKey(
                name: "FK_DailyUsageCounters_Users_UserId",
                table: "DailyUsageCounters");

            migrationBuilder.DropForeignKey(
                name: "FK_Questions_Quizzes_QuizId",
                table: "Questions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_Quizzes_QuizId",
                table: "QuizAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_QuizAttempts_Users_UserId",
                table: "QuizAttempts");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Contents_ContentId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes");

            migrationBuilder.DropForeignKey(
                name: "FK_StudyStatistics_Users_UserId",
                table: "StudyStatistics");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemSettings_Users_UpdatedByUserId",
                table: "SystemSettings");

            migrationBuilder.DropForeignKey(
                name: "FK_UserAnswers_QuizAttempts_AttemptId",
                table: "UserAnswers");

            migrationBuilder.AddForeignKey(
                name: "FK_AIProcesses_Contents_ContentId",
                table: "AIProcesses",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "ContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AISystemLogs_Users_UserId",
                table: "AISystemLogs",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentModerations_Contents_ContentId",
                table: "ContentModerations",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "ContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_ContentModerations_Users_ReviewedByUserId",
                table: "ContentModerations",
                column: "ReviewedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contents_Users_UserId",
                table: "Contents",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyUsageCounters_GuestSessions_GuestSessionId",
                table: "DailyUsageCounters",
                column: "GuestSessionId",
                principalTable: "GuestSessions",
                principalColumn: "GuestSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_DailyUsageCounters_Users_UserId",
                table: "DailyUsageCounters",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Questions_Quizzes_QuizId",
                table: "Questions",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "QuizId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAttempts_Quizzes_QuizId",
                table: "QuizAttempts",
                column: "QuizId",
                principalTable: "Quizzes",
                principalColumn: "QuizId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuizAttempts_Users_UserId",
                table: "QuizAttempts",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Contents_ContentId",
                table: "Quizzes",
                column: "ContentId",
                principalTable: "Contents",
                principalColumn: "ContentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Quizzes_Users_UserId",
                table: "Quizzes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_StudyStatistics_Users_UserId",
                table: "StudyStatistics",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemSettings_Users_UpdatedByUserId",
                table: "SystemSettings",
                column: "UpdatedByUserId",
                principalTable: "Users",
                principalColumn: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_UserAnswers_QuizAttempts_AttemptId",
                table: "UserAnswers",
                column: "AttemptId",
                principalTable: "QuizAttempts",
                principalColumn: "AttemptId");
        }
    }
}
