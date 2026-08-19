import { Component } from '@angular/core';
import { Router } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.css']
})
export class AppComponent {
  title = 'Employee Directory';
  subtitle = 'Employee Management Portal';
  subsubtitle = 'MS-SQL + ASP.NET Core + Angular';

  constructor(public router: Router) { }
}
