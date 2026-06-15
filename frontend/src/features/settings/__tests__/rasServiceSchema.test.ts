import { describe, expect, it } from "vitest";
import { rasServiceStatusSchema, rasServiceOperationSchema } from "../useRasService";

/**
 * Parity BE↔FE для контракта службы RAS (MLC-160, ADR-47).
 *
 * Регрессия [[api-omits-null-fields]]: backend сериализует null-поля ПРОПУСКОМ ключа
 * (JsonIgnoreCondition.WhenWritingNull). У ответа /ras-service/status опускаются
 * service/target/commandPreview/issue (и вложенные best-effort binPath/platformVersion/
 * port у service). Схема обязана принимать оба варианта (отсутствие ключа / null) —
 * иначе Zod-граница отвергнет валидный ответ и блок упадёт в ошибку.
 */
describe("rasServiceStatusSchema — omit-null parity (MLC-160)", () => {
  it("NotRegistered: service/target/issue опущены — приходят только обязательные поля", () => {
    // Ровно как с провода для «чистого сервера»: службы нет, окружение готово,
    // commandPreview есть (предпросмотр register), всё nullable опущено.
    const raw = {
      state: "NotRegistered",
      targetReady: true,
      commandPreview: 'sc create MitLicenseRas binPath= "..."',
      target: {
        rasExePath: "C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\ras.exe",
        platformVersion: "8.3.23.1865",
        port: "1545",
        agentAddress: "localhost:1540",
      },
    };
    const parsed = rasServiceStatusSchema.parse(raw);
    expect(parsed.state).toBe("NotRegistered");
    expect(parsed.service).toBeNull();
    expect(parsed.issue).toBeNull();
    expect(parsed.target?.platformVersion).toBe("8.3.23.1865");
  });

  it("targetReady=false: target/commandPreview опущены, issue заполнено", () => {
    const parsed = rasServiceStatusSchema.parse({
      state: "NotRegistered",
      targetReady: false,
      issue: "Не выбрана платформа 1С — задайте путь к rac.exe.",
    });
    expect(parsed.targetReady).toBe(false);
    expect(parsed.issue).toContain("платформа");
    expect(parsed.target).toBeNull();
    expect(parsed.commandPreview).toBeNull();
  });

  it("Ok с полностью заполненной обнаруженной службой", () => {
    const parsed = rasServiceStatusSchema.parse({
      state: "Ok",
      targetReady: true,
      service: {
        serviceName: "MitLicenseRas",
        isRunning: true,
        binPath: "C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\ras.exe",
        platformVersion: "8.3.23.1865",
        port: "1545",
      },
      target: {
        rasExePath: "C:\\Program Files\\1cv8\\8.3.23.1865\\bin\\ras.exe",
        platformVersion: "8.3.23.1865",
        port: "1545",
        agentAddress: "localhost:1540",
      },
    });
    expect(parsed.service?.isRunning).toBe(true);
    expect(parsed.service?.serviceName).toBe("MitLicenseRas");
  });

  it("Outdated: служба с непарсимым выводом sc — best-effort поля service опущены", () => {
    const parsed = rasServiceStatusSchema.parse({
      state: "Outdated",
      targetReady: true,
      service: { serviceName: "RAS1C", isRunning: true },
      target: {
        rasExePath: "C:\\Program Files\\1cv8\\8.5.1.1302\\bin\\ras.exe",
        platformVersion: "8.5.1.1302",
        port: "1545",
        agentAddress: "localhost:1540",
      },
      commandPreview: "sc config RAS1C binPath= ...",
    });
    expect(parsed.service?.binPath).toBeNull();
    expect(parsed.service?.platformVersion).toBeNull();
    expect(parsed.service?.port).toBeNull();
  });

  it("отвергает ответ без обязательного targetReady", () => {
    expect(() => rasServiceStatusSchema.parse({ state: "Ok" })).toThrow();
  });
});

describe("rasServiceOperationSchema (MLC-160)", () => {
  it("принимает ответ операции", () => {
    const parsed = rasServiceOperationSchema.parse({
      state: "Ok",
      serviceName: "MitLicenseRas",
    });
    expect(parsed.serviceName).toBe("MitLicenseRas");
  });
});
