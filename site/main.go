// Генератор публичного сайта afk4.net (docs/operations/public-site.md): четыре страницы на трёх языках в site/dist.
// Go, html/template и embed, без внешних зависимостей: сайт должен открываться на плохом канале клуба.
//
//	cd site && go run .
package main

import (
	"embed"
	"encoding/json"
	"fmt"
	"html/template"
	"io/fs"
	"log"
	"net/url"
	"os"
	"path/filepath"
	"strings"
	"time"
)

//go:embed templates/*.gohtml static content/*.json
var assets embed.FS

var langs = []string{"ru", "tg", "en"}

var pages = []string{"index", "download", "code-signing", "privacy"}

// Seat — одно место на карте зала (пример на выдуманном клубе).
type Seat struct {
	N       string
	State   string
	Seconds int
}

var seats = []Seat{
	{"01", "play", 2592}, {"02", "free", 0}, {"03", "play", 1214}, {"04", "play", 5410},
	{"05", "play", 289}, {"06", "free", 0}, {"07", "play", 3300}, {"08", "offline", 0},
	{"09", "play", 905}, {"10", "play", 4120}, {"11", "free", 0}, {"12", "play", 1750},
}

type Feature struct{ Title, Body, When string }

type Content struct {
	HTMLLang string
	LangName string
	Nav      struct{ Home, Download, Demo, Contact, Features, Signing, Privacy string }
	Meta     struct{ Title, Description string }
	Footer   struct{ Tagline, License, Contact, Product, Docs, Made, Windows string }
	Download struct {
		Title, Lead, Button, ButtonNote, RequirementsTitle string
		Requirements                                       []string
		ChangesTitle, ChangesLead                          string
		Changes                                            []Feature
		DataTitle, DataBody, UninstallTitle                string
		UninstallSteps                                     []string
		UninstallLeftovers                                 string
	}
	Signing struct {
		Title, Lead, Attribution, WhatTitle, WhatBody, RolesTitle string
		Roles                                                     []Feature
		ProcessTitle                                              string
		Process                                                   []string
		PrivacyTitle, PrivacyBody                                 string
	}
	Privacy struct {
		Title, Updated string
		Sections       []Feature
	}
	Home Home
}

type Tile struct{ Title, Text string }

type Group struct {
	Title, Status string
	Items         []string
}

type Home struct {
	Choose struct {
		Title string
		Items []string
	}
	Hero struct {
		Title, Lead, NameLine, Write, Demo, ImageAlt string
	}
	Board struct {
		Label, Synthetic, Have, Planned string
		Map                             struct {
			Tile
			Free, Left, Offline, PC, Hint string
			Actions                       struct{ Lock, Unlock, Message string }
			Sent                          string
		}
		Till struct {
			Tile
			Lines           []struct{ Name, Price string }
			Total, Currency string
		}
		Guest struct {
			Tile
			ImageAlt string
		}
		App struct {
			Tile
			Booking, TopUp, Scan, Balance, Club string
			Features                            []string
		}
		Telegram struct {
			Tile
			Messages []string
		}
		Offline struct {
			Tile
			Button, ButtonBack, On, Off, CashOnly, Counting string
		}
		Cache struct {
			Tile
			Done, Pcs, Internet, Admin, Once string
		}
		Langs struct {
			Tile
			Phrases struct{ Tg, Ru, En string }
		}
		Soon struct {
			Tile
			Items []string
		}
	}
	Inside struct {
		Title, Lead string
		Groups      []Group
	}
	UI struct {
		PaletteOpen, PalettePlaceholder, PaletteHint, PaletteEmpty, Next, Language string
	}
	How struct {
		Title string
		Steps []Feature
		CTA   string
	}
	Open    struct{ Title, Body, Link string }
	Contact struct {
		Title, Lead, Name, Phone, Send, Sending, Sent, Error, Cta, PopupTitle, PopupLead, Close string
		Channels, Telegram, Call, Email, Wait                                                   string
	}
}

type Links struct {
	Links struct {
		Repo, Releases, Members, Owner, Demo, Signpath, SignpathFoundation, Email string
	}
	Contacts struct {
		Telegram, TelegramHandle, Phone, PhoneShown string
	}
	ContactEndpoint string
}

// LangLink — ссылка переключателя языка.
type LangLink struct {
	Code, HTMLLang, Name, Href string
	Current                    bool
}

// View — всё, что нужно шаблону страницы.
type View struct {
	Lang     string
	Page     string
	C        *Content
	L        Links
	Seats    []Seat
	Switcher []LangLink
	Title    string
	Desc     string
	Alts     []LangLink
	Year     int
}

func prefix(lang string) string {
	if lang == "ru" {
		return "/"
	}
	return "/" + lang + "/"
}

func href(lang, page string) string {
	if page == "index" {
		return prefix(lang)
	}
	return prefix(lang) + page + ".html"
}

func clock(total int) string {
	h, m, s := total/3600, total%3600/60, total%60
	if h > 0 {
		return fmt.Sprintf("%d:%02d:%02d", h, m, s)
	}
	return fmt.Sprintf("%02d:%02d", m, s)
}

func sumLines(lines []struct{ Name, Price string }) int {
	total := 0
	for _, line := range lines {
		var n int
		fmt.Sscanf(line.Price, "%d", &n)
		total += n
	}
	return total
}

func must(err error) {
	if err != nil {
		log.Fatal(err)
	}
}

func load[T any](name string) T {
	var out T
	raw, err := assets.ReadFile(name)
	must(err)
	must(json.Unmarshal(raw, &out))
	return out
}

func copyEmbedded(dir, dst string) {
	must(fs.WalkDir(assets, dir, func(path string, d fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		rel := strings.TrimPrefix(strings.TrimPrefix(path, dir), "/")
		target := filepath.Join(dst, rel)
		if d.IsDir() {
			return os.MkdirAll(target, 0o755)
		}
		data, err := assets.ReadFile(path)
		if err != nil {
			return err
		}
		return os.WriteFile(target, data, 0o644)
	}))
}

func copyFile(src, dst string) {
	data, err := os.ReadFile(src)
	must(err)
	must(os.WriteFile(dst, data, 0o644))
}

func main() {
	root := "."
	if _, err := os.Stat("templates"); err != nil {
		root = "site"
	}
	dist := filepath.Join(root, "dist")
	must(os.RemoveAll(dist))

	links := load[Links]("content/links.json")
	contents := map[string]*Content{}
	for _, lang := range langs {
		c := load[Content]("content/" + lang + ".json")
		// Разделы с пометкой when=form описывают форму связи: пока у сайта нет адреса в API (contactEndpoint), их нет.
		if links.ContactEndpoint == "" {
			kept := c.Privacy.Sections[:0]
			for _, section := range c.Privacy.Sections {
				if section.When != "form" {
					kept = append(kept, section)
				}
			}
			c.Privacy.Sections = kept
		}
		contents[lang] = &c
	}

	funcs := template.FuncMap{
		"href":   href,
		"clock":  clock,
		"sum":    sumLines,
		"icon":   icon,
		"cicon":  cicon,
		"anchor": func(lang, a string) string { return href(lang, "index") + "#" + a },
		"mail":   func(s string) string { return strings.TrimPrefix(s, "mailto:") },
		"host":   func(s string) string { return strings.TrimPrefix(s, "https://") },
		"seq":    func(items []Seat, n int) []Seat { return items[:n] },
	}
	base := template.Must(template.New("base").Funcs(funcs).ParseFS(assets, "templates/layout.gohtml"))

	for _, lang := range langs {
		c := contents[lang]
		dir := dist
		if lang != "ru" {
			dir = filepath.Join(dist, lang)
		}
		must(os.MkdirAll(dir, 0o755))
		for _, page := range pages {
			t := template.Must(template.Must(base.Clone()).ParseFS(assets, "templates/"+page+".gohtml"))
			view := View{Lang: lang, Page: page, C: c, L: links, Seats: seats, Year: time.Now().Year()}
			switch page {
			case "index":
				view.Title, view.Desc = c.Meta.Title, c.Meta.Description
			case "download":
				view.Title, view.Desc = c.Download.Title+" · AFK4.NET", c.Meta.Description
			case "code-signing":
				view.Title, view.Desc = c.Signing.Title+" · AFK4.NET", c.Meta.Description
			case "privacy":
				view.Title, view.Desc = c.Privacy.Title+" · AFK4.NET", c.Meta.Description
			}
			for _, other := range langs {
				oc := contents[other]
				link := LangLink{Code: other, HTMLLang: oc.HTMLLang, Name: oc.LangName, Href: href(other, page), Current: other == lang}
				view.Switcher = append(view.Switcher, link)
			}
			file, err := os.Create(filepath.Join(dir, page+".html"))
			must(err)
			must(t.ExecuteTemplate(file, "layout", view))
			must(file.Close())
		}
	}

	copyEmbedded("static", dist)
	// Токены и логотип берём из канона продукта, а не копируем руками: сайт идёт за ними.
	copyFile(filepath.Join(root, "..", "packages", "tokens", "tokens.css"), filepath.Join(dist, "tokens.css"))
	copyFile(filepath.Join(root, "..", "brand", "afk4-logo-horizontal.svg"), filepath.Join(dist, "img", "logo.svg"))
	copyFile(filepath.Join(root, "..", "brand", "afk4-icon.svg"), filepath.Join(dist, "img", "favicon.svg"))
	// Форма связи ходит в API по другому адресу: его, и только его, разрешаем в connect-src.
	connect := "'self'"
	if api, err := url.Parse(links.ContactEndpoint); err == nil && api.Host != "" {
		connect += " " + api.Scheme + "://" + api.Host
	}
	headers := "/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: strict-origin-when-cross-origin\n" +
		"  Content-Security-Policy: default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; connect-src " + connect + "; form-action 'self'; base-uri 'self'; frame-ancestors 'none'\n"
	must(os.WriteFile(filepath.Join(dist, "_headers"), []byte(headers), 0o644))
	fmt.Printf("site built: %d languages × %d pages → %s\n", len(langs), len(pages), dist)
}

// groupIcons — значки групп возможностей в том же порядке, что группы в content/*.json: один штрих, 24 px.
var groupIcons = []string{
	`<rect x="3" y="3" width="7" height="7" rx="1.5"/><rect x="14" y="3" width="7" height="7" rx="1.5"/><rect x="3" y="14" width="7" height="7" rx="1.5"/><rect x="14" y="14" width="7" height="7" rx="1.5"/>`,
	`<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>`,
	`<path d="M6 3h12v18l-3-2-3 2-3-2-3 2z"/><path d="M9 8h6M9 12h6"/>`,
	`<circle cx="9" cy="8" r="3.5"/><path d="M2.5 20c.6-3.6 3.2-5.5 6.5-5.5s5.9 1.9 6.5 5.5"/><path d="M16 4.6a3.5 3.5 0 0 1 0 6.8M18.5 14.8c1.6.8 2.7 2.4 3 5.2"/>`,
	`<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>`,
	`<path d="M4 20V10M10 20V4M16 20v-8M22 20H2"/>`,
	`<path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"/><path d="M9 12l2 2 4-4"/>`,
	`<path d="M9 3v5M15 3v5M7 8h10v3a5 5 0 0 1-10 0zM12 16v5"/>`,
	`<rect x="7" y="2.5" width="10" height="19" rx="2.5"/><path d="M11 18.5h2"/>`,
	`<path d="M10 14a4 4 0 0 0 5.7 0l3-3a4 4 0 0 0-5.7-5.7l-1 1"/><path d="M14 10a4 4 0 0 0-5.7 0l-3 3a4 4 0 0 0 5.7 5.7l1-1"/>`,
	`<path d="M5 8h12v5a5 5 0 0 1-5 5h-2a5 5 0 0 1-5-5z"/><path d="M17 9h2a2 2 0 0 1 0 4h-2M8 3v2M12 3v2"/>`,
	`<path d="M12 3v12M7 11l5 5 5-5M4 21h16"/>`,
}

func icon(i int) template.HTML {
	if i < 0 || i >= len(groupIcons) {
		return ""
	}
	return template.HTML(`<svg class="gicon" viewBox="0 0 24 24" width="24" height="24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">` + groupIcons[i] + `</svg>`)
}

// changeIcons — значки пунктов «Что установка меняет в Windows» в порядке content/*.json.
var changeIcons = []string{
	`<path d="M9 3v5M15 3v5M7 8h10v3a5 5 0 0 1-10 0zM12 16v5"/>`,
	`<rect x="3" y="4" width="18" height="12" rx="2"/><path d="M8 20h8M12 16v4"/>`,
	`<path d="M12 3l8 3v6c0 5-3.5 8-8 9-4.5-1-8-4-8-9V6z"/><path d="M9 12l2 2 4-4"/>`,
	`<path d="M14 3H7a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h10a2 2 0 0 0 2-2V8z"/><path d="M14 3v5h5M9 13h6M9 17h4"/>`,
	`<path d="M12 3v9"/><path d="M6.3 6.8a8 8 0 1 0 11.4 0"/>`,
	`<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3 2"/>`,
}

func cicon(i int) template.HTML {
	if i < 0 || i >= len(changeIcons) {
		return ""
	}
	return template.HTML(`<svg class="gicon" viewBox="0 0 24 24" width="26" height="26" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">` + changeIcons[i] + `</svg>`)
}
