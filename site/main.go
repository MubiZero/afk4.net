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

type Feature struct{ Title, Body string }

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
		Title, Lead, Name, Club, Phone, Message, Send, Sending, Sent, Error, Consent string
		Channels, Telegram, Call, Email, Wait                                        string
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
		contents[lang] = &c
	}

	funcs := template.FuncMap{
		"href":    href,
		"clock":   clock,
		"sum":     sumLines,
		"anchor":  func(lang, a string) string { return href(lang, "index") + "#" + a },
		"mail":    func(s string) string { return strings.TrimPrefix(s, "mailto:") },
		"host":    func(s string) string { return strings.TrimPrefix(s, "https://") },
		"seq":     func(items []Seat, n int) []Seat { return items[:n] },
		"isfocus": func(s string) bool { return s == "" },
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
	headers := "/*\n  X-Content-Type-Options: nosniff\n  Referrer-Policy: strict-origin-when-cross-origin\n" +
		"  Content-Security-Policy: default-src 'self'; img-src 'self'; style-src 'self'; script-src 'self'; connect-src 'self'; form-action 'self'; base-uri 'self'; frame-ancestors 'none'\n"
	must(os.WriteFile(filepath.Join(dist, "_headers"), []byte(headers), 0o644))
	fmt.Printf("site built: %d languages × %d pages → %s\n", len(langs), len(pages), dist)
}
