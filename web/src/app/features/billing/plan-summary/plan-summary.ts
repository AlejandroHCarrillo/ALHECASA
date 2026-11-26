import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { AccountBilling, describeUsage, usagePercent } from '../billing.models';

/** Lo que tiene contratada la cuenta y lo que lleva usado: publicaciones y destacados. Se usa en «Inicio» y en «Mi plan». */
@Component({
  selector: 'app-plan-summary',
  imports: [DatePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './plan-summary.html',
})
export class PlanSummary {
  readonly account = input.required<AccountBilling>();

  protected readonly describeUsage = describeUsage;
  protected readonly usagePercent = usagePercent;
}
